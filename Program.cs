﻿using OpenAI.GPT3.Managers;
using OpenAI.GPT3.ObjectModels;
using OpenAI.GPT3.ObjectModels.RequestModels;
using YoutubeExplode;

using System.Reflection;

namespace TeleprompterConsole;

internal class Program
{

    public static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            var versionString = Assembly.GetEntryAssembly()?
                                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                                    .InformationalVersion
                                    .ToString();
            Console.WriteLine($"timestamper v{versionString}");
            Console.WriteLine("-------------");
            Console.WriteLine("\nUsage:");
            Console.WriteLine("  timestamper <videoUrl> <number of timestamps>");
            return;
        }

        string? apiKey = "";

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("openai_api_key")))
        {
            Console.WriteLine("Please set the openai_api_key environment variable");
            return;
        }
        else
        {
            apiKey = Environment.GetEnvironmentVariable("openai_api_key");
            YoutubeExplode.Videos.ClosedCaptions.ClosedCaptionTrack track = await SetUpServices(args[0], Int32.Parse(args[1]));
            await GenerateCaptions(args[0], Int32.Parse(args[1]), track, apiKey!);
        }


    }

    static async Task<YoutubeExplode.Videos.ClosedCaptions.ClosedCaptionTrack> SetUpServices(string videoUrl, int slices)
    {
        var youtube = new YoutubeClient();

        var trackManifest = await youtube.Videos.ClosedCaptions.GetManifestAsync(
            videoUrl
        );
        var trackInfo = trackManifest.GetByLanguage("en");
        var track = await youtube.Videos.ClosedCaptions.GetAsync(trackInfo);

        return track;
    }

    static async Task GenerateCaptions(string videoUrl, int slices, YoutubeExplode.Videos.ClosedCaptions.ClosedCaptionTrack track, string apiKey)
    {
        Console.WriteLine($"Generating {slices} timestamps for " + videoUrl);

        int captionsPerSlice = track.Captions.Count / slices;
        int startIndex = 0;
        int endIndex = captionsPerSlice;
        string captions = "";
        string summary = "";



        var openAiService = new OpenAIService(new OpenAI.GPT3.OpenAiOptions()
        {
            ApiKey = apiKey
        });

        for (int l = 0; l < slices; l++)
        {
            var caption = track.Captions[startIndex];

            Console.Write(caption.Offset.ToString().Split('.')[0] + " ");
            captions = "";

            for (int k = startIndex; k < endIndex; k++)
            {
                caption = track.Captions[k];
                if (!string.IsNullOrWhiteSpace(caption.Text))
                {
                    captions += $"{caption.Text}";
                }
            }


            // Print number of chars in string
            /// Console.WriteLine($"Characters in caption: +{captions.Length}");
            //Console.WriteLine($"Max tokens to use: +{captions.Length/4}");

            var completionResult = await openAiService.Completions.CreateCompletion(new CompletionCreateRequest()
            {
                Prompt = $"(Tell me the main idea of the following text in 15 words: {captions}",
                Model = Models.TextDavinciV3,
                Temperature = (float?)0.7,
                TopP = 1,
                MaxTokens = captions.Length / 4,
                FrequencyPenalty = (float?)0.93,
                PresencePenalty = (float?)1.03
            });

            if (completionResult.Successful)
            {
                summary = "";

                if (completionResult.Choices.Count == 0)
                {
                    throw new Exception("No Choices");
                } else
                {
                    summary = completionResult.Choices.FirstOrDefault()!.ToString().Remove(0, 25);
                }
                
                //Console.WriteLine($"Tokens used: {completionResult.Usage.TotalTokens}");
                int index = summary.IndexOf("Index =");
                if (index >= 0)
                {
                    summary = summary.Substring(0, index);
                }
                Console.WriteLine(summary);
            }
            else
            {
                if (completionResult.Error == null)
                {
                    throw new Exception("Unknown Error");
                }
                Console.WriteLine($"{completionResult.Error.Code}: {completionResult.Error.Message}");
            }
            if (endIndex + captionsPerSlice < track.Captions.Count)
            {
                startIndex = startIndex + captionsPerSlice;
                endIndex = endIndex + captionsPerSlice;
            }
            else
            {
                startIndex = endIndex;
            }
        }
    }
}