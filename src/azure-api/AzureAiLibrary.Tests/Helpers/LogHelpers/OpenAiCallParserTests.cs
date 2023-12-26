using AzureAiLibrary.Helpers.LogHelpers;

namespace AzureAiLibrary.Tests.Helpers.LogHelpers
{
    public class OpenAiCallParserTests
    {
        private readonly OpenAICallParser _sut;

        public OpenAiCallParserTests()
        {
            _sut = new OpenAICallParser();
        }

        [Fact]
        public void CanParseStandardRequest()
        {
            const string data = "{\"messages\":[{\"content\":\"I want to extract audio from video file C:\\\\temp\\\\230Github.mp4\",\"role\":\"user\"}],\"temperature\":1,\"top_p\":1,\"n\":1,\"presence_penalty\":0,\"frequency_penalty\":0,\"model\":\"gpt4t\",\"tools\":[{\"function\":{\"name\":\"AudioVideoPlugin_ExtractAudio\",\"description\":\"extract audio in wav format from an mp4 file\",\"parameters\":{\"type\":\"object\",\"required\":[\"videofile\"],\"properties\":{\"videofile\":{\"type\":\"string\",\"description\":\"Full path to the mp4 file\"}}}},\"type\":\"function\"},{\"function\":{\"name\":\"AudioVideoPlugin_TranscriptTimeline\",\"description\":\"Transcript audio from a wav file to a timeline extracting a transcript\",\"parameters\":{\"type\":\"object\",\"required\":[\"audioFile\"],\"properties\":{\"audioFile\":{\"type\":\"string\",\"description\":\"Full path to the wav file\"}}}},\"type\":\"function\"},{\"function\":{\"name\":\"PublishingPlugin_VideoTimelineCreator\",\"description\":\"Given a video transcript it can summarize and generate a timeline\",\"parameters\":{\"type\":\"object\",\"required\":[\"transcript\"],\"properties\":{\"transcript\":{\"type\":\"string\",\"description\":\"Transcript of an audio file with time markers\"}}}},\"type\":\"function\"}],\"tool_choice\":\"auto\"}";
            var result = _sut.ParseRequest(data);
            Assert.Contains("I want to extract audio from video file", result.PromptSequence);
        }

        [Fact]
        public void CanParseRequestWithMultiplePrompts()
        {
            const string data = "{\"messages\":[{\"content\":\"I want to extract audio from video file C:\\\\temp\\\\230Github.mp4\",\"role\":\"user\"},{\"content\":null,\"tool_calls\":[{\"function\":{\"name\":\"AudioVideoPlugin_ExtractAudio\",\"arguments\":\"{\\u0022videofile\\u0022:\\u0022C:\\\\\\\\temp\\\\\\\\230Github.mp4\\u0022}\"},\"type\":\"function\",\"id\":\"call_tc5BwWQOfjk44h4pvOG2VmY5\"}],\"role\":\"assistant\"},{\"content\":\"C:\\\\temp\\\\230Github.wav\",\"tool_call_id\":\"call_tc5BwWQOfjk44h4pvOG2VmY5\",\"role\":\"tool\"}],\"temperature\":1,\"top_p\":1,\"n\":1,\"presence_penalty\":0,\"frequency_penalty\":0,\"model\":\"gpt4t\",\"tools\":[{\"function\":{\"name\":\"AudioVideoPlugin_ExtractAudio\",\"description\":\"extract audio in wav format from an mp4 file\",\"parameters\":{\"type\":\"object\",\"required\":[\"videofile\"],\"properties\":{\"videofile\":{\"type\":\"string\",\"description\":\"Full path to the mp4 file\"}}}},\"type\":\"function\"},{\"function\":{\"name\":\"AudioVideoPlugin_TranscriptTimeline\",\"description\":\"Transcript audio from a wav file to a timeline extracting a transcript\",\"parameters\":{\"type\":\"object\",\"required\":[\"audioFile\"],\"properties\":{\"audioFile\":{\"type\":\"string\",\"description\":\"Full path to the wav file\"}}}},\"type\":\"function\"},{\"function\":{\"name\":\"PublishingPlugin_VideoTimelineCreator\",\"description\":\"Given a video transcript it can summarize and generate a timeline\",\"parameters\":{\"type\":\"object\",\"required\":[\"transcript\"],\"properties\":{\"transcript\":{\"type\":\"string\",\"description\":\"Transcript of an audio file with time markers\"}}}},\"type\":\"function\"}],\"tool_choice\":\"auto\"}";
            var result = _sut.ParseRequest(data);
            Assert.Contains("I want to extract audio from video file", result.PromptSequence);
            Assert.Contains("Tool call: AudioVideoPlugin_ExtractAudio", result.PromptSequence);
            Assert.Contains("Tool answer: AudioVideoPlugin_ExtractAudio -> C:\\temp\\230Github.wav", result.PromptSequence);
        }

        [Fact]
        public void CanParseBasicResponse()
        {
            const string data = "{\"id\":\"chatcmpl-8b2KzJHSlYiCEew3MaYr9BCKnbSbo\",\"object\":\"chat.completion\",\"created\":1703837813,\"model\":\"gpt-4\",\"prompt_filter_results\":[{\"prompt_index\":0,\"content_filter_results\":{\"hate\":{\"filtered\":false,\"severity\":\"safe\"},\"self_harm\":{\"filtered\":false,\"severity\":\"safe\"},\"sexual\":{\"filtered\":false,\"severity\":\"safe\"},\"violence\":{\"filtered\":false,\"severity\":\"safe\"}}}],\"choices\":[{\"finish_reason\":\"tool_calls\",\"index\":0,\"message\":{\"role\":\"assistant\",\"tool_calls\":[{\"id\":\"call_WEzXJAJ8gVuTJaZpp1jmVBs6\",\"type\":\"function\",\"function\":{\"name\":\"AudioVideoPlugin_ExtractAudio\",\"arguments\":\"{\\\"videofile\\\":\\\"C:\\\\\\\\temp\\\\\\\\230Github.mp4\\\"}\"}}]},\"content_filter_results\":{}}],\"usage\":{\"prompt_tokens\":165,\"completion_tokens\":26,\"total_tokens\":191},\"system_fingerprint\":\"fp_50a4261de5\"}";
            var result = _sut.ParseResponse(data);
            Assert.Contains("AudioVideoPlugin_ExtractAudio", result.FunctionCall);
            Assert.Contains("230Github.mp4", result.FunctionArguments);
            Assert.Equal("tool_calls", result.FinishReason);
            Assert.Equal("gpt-4", result.Model);    
        }

        [Fact]
        public void CanParseStopResponse()
        {
            const string data = "{\"id\":\"chatcmpl-8b37iSJMO48xVE8nEh7d3CiCg0ZGG\",\"object\":\"chat.completion\",\"created\":1703840834,\"model\":\"gpt-4\",\"prompt_filter_results\":[{\"prompt_index\":0,\"content_filter_results\":{\"hate\":{\"filtered\":false,\"severity\":\"safe\"},\"self_harm\":{\"filtered\":false,\"severity\":\"safe\"},\"sexual\":{\"filtered\":false,\"severity\":\"safe\"},\"violence\":{\"filtered\":false,\"severity\":\"safe\"}}}],\"choices\":[{\"index\":0,\"finish_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":\"The audio has been successfully extracted from the video file \\\"C:\\\\temp\\\\230Github.mp4\\\" and is now available as a WAV file at the following location: \\\"C:\\\\temp\\\\230Github.wav\\\".\"},\"content_filter_results\":{\"hate\":{\"filtered\":false,\"severity\":\"safe\"},\"self_harm\":{\"filtered\":false,\"severity\":\"safe\"},\"sexual\":{\"filtered\":false,\"severity\":\"safe\"},\"violence\":{\"filtered\":false,\"severity\":\"safe\"}}}],\"usage\":{\"prompt_tokens\":210,\"completion_tokens\":43,\"total_tokens\":253},\"system_fingerprint\":\"fp_50a4261de5\"}";
            var result = _sut.ParseResponse(data);
            Assert.Equal("stop", result.FinishReason);
            Assert.Empty(result.FunctionCall);
            Assert.Empty(result.FunctionArguments);
            Assert.NotNull(result.Answer);
            Assert.Equal("The audio has been successfully extracted from the video file \"C:\\temp\\230Github.mp4\" and is now available as a WAV file at the following location: \"C:\\temp\\230Github.wav\".", result.Answer);
        }
    }
}
