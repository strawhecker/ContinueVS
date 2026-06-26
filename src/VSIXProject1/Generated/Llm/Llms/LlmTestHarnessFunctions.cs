namespace ContinueCore.Llm.Llms;
public static partial class LlmTestHarnessFunctions
{
    public static async Task runLlmTest(LlmTestCase testCase)
    {
        var {
    llm,
    methodToTest,
    params,
    expectedRequest,
    mockResponse,
    mockStream,
  } = testCase;
        var mockFetch = jest.fn();
        if (mockStream)
        {
            var encoder = "/* unknown: new TextEncoder() */";
            var streamIndex = 0L;
            mockFetch.mockResolvedValue("/* unknown: new Response(\r\n        new ReadableStream({\r\n          start(controller) {\r\n            mockStream.forEach((chunk) => {\r\n              controller.enqueue(new TextEncoder().encode(chunk));\r\n            });\r\n            controller.close();\r\n          },\r\n        }),\r\n        {\r\n          headers: {\r\n            \"Content-Type\": \"text/event-stream\",\r\n          },\r\n        },\r\n      ) */");
        }
        else
        {
            mockFetch.mockResolvedValue("/* unknown: new Response(JSON.stringify(mockResponse), {\r\n        headers: {\r\n          \"Content-Type\": \"application/json\",\r\n        },\r\n      }) */");
        }

        llm.fetch = mockFetch;
        if ("/* unknown: llm[methodToTest] */" is not System.Delegate)
        {
            throw "/* unknown: new Error(\r\n      `Method ${String(methodToTest)} does not exist on the LLM instance.`,\r\n    ) */";
        }

        var result = await "/* unknown: llm[methodToTest] */"("/* unknown: ...params */");
        if (mockStream)
        {
            foreach (var const _ in result)
            {
            }
        }

        expect(mockFetch).toHaveBeenCalledTimes(1L);
        var [url, options] = "/* unknown: mockFetch.mock.calls[0] as [string, RequestInit] */";
        expect(url.toString()).toBe(expectedRequest.url);
        expect(options.method).toBe(expectedRequest.method);
        if (expectedRequest.headers)
        {
            expect(options.headers).toEqual(expectedRequest.headers);
        }

        if (expectedRequest.body)
        {
            var actualBody = System.Text.Json.JsonSerializer.Deserialize("/* unknown: options.body as string */");
            expect(actualBody).toEqual(expectedRequest.body);
        }
    }
}