
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Http;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.SemanticKernel.Skills.OpenAPI.Extensions;

namespace SKonsole.Commands;

public static class KernelAIPluginsExtensions
{
    public static async Task<IDictionary<string, ISKFunction>> ImportAIPluginsAsync(this IKernel kernel, Uri uri, OpenApiSkillExecutionParameters? executionParameters = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        // Microsoft.SemanticKernel.Diagnostics.Verify.NotNull(kernel, "kernel");
        // Microsoft.SemanticKernel.Diagnostics.Verify.ValidSkillName(skillName);
        HttpClient httpClient = HttpClientProvider.GetHttpClient(kernel.HttpHandlerFactory, executionParameters?.HttpClient, kernel.LoggerFactory);
        var pluginListString = await LoadDocumentFromUri(kernel, uri, executionParameters, httpClient, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        // var pluginList = JsonSerializer.Deserialize<List<object>>(pluginListString);
        // Deserialize the JSON to a JsonDocument
        JsonDocument jsonDocument = JsonDocument.Parse(pluginListString);

        // Extract the "plugins" array
        JsonElement pluginsArray = jsonDocument.RootElement.GetProperty("plugins");

        // Initialize a list to hold the raw JSON strings
        List<string> rawJsonStrings = new();

        // Iterate through the elements in the "plugins" array
        foreach (JsonElement element in pluginsArray.EnumerateArray())
        {
            // Convert the element to a JSON string
            string jsonString = element.ToString();
            rawJsonStrings.Add(jsonString);
        }

        // foreach plugin in plugins
        //   get plugin.ai-plugin.json
        //   import plugin.ai-plugin.json

        Dictionary<string, ISKFunction> functions = new();
        foreach (var jsonPlugin in rawJsonStrings!)
        {
            // Deserialize the JSON object
            var jsonObject = JsonSerializer.Deserialize<JsonElement>(jsonPlugin);

            // Initialize the name variable with "manifest.name_for_model"
            string? name = jsonObject.GetProperty("manifest").GetProperty("name_for_model").GetString();

            // If "manifest.name_for_model" is not present, use "name"
            if (string.IsNullOrEmpty(name))
            {
                name = jsonObject.GetProperty("name").GetString();
            }

            // If "name" is not present, use "openapi.info.title"
            if (string.IsNullOrEmpty(name))
            {
                name = jsonObject.GetProperty("openapi").GetProperty("info").GetProperty("title").GetString();
            }


            // Check if we have a valid name
            if (!string.IsNullOrEmpty(name))
            {
                string? manifest = jsonObject.GetProperty("manifest").ToString();

                // convert obj to a string as a Stream
                byte[] byteArray = Encoding.UTF8.GetBytes(manifest ?? jsonPlugin);

                // Create a MemoryStream from the byte array
                using (MemoryStream stream = new(byteArray))
                {
                    // 'A skill name can contain only ASCII letters, digits, and underscores: 'apis.guru' is not a valid name.'
                    // TODO SK Bug -- allow more?
                    // normalize name
                    name = name.Replace(".", "_").Replace("-", "_").Replace(" ", "_").Replace("/", "_").Replace("\\", "_").Replace(":", "_").Replace(";", "_");

                    try
                    {
                        var f = await kernel.ImportAIPluginAsync(name, stream, executionParameters, cancellationToken);

                        foreach (var item in f)
                        {
                            functions.Add(item.Key, item.Value);
                        }
                    }
                    catch (Exception ex)
                    {
                        kernel.LoggerFactory.CreateLogger(typeof(KernelAIPluginsExtensions)).LogError(ex, "ImportAIPluginsAsync: {0}", name);
                    }
                }
            }
        }
        return functions;
    }

    private static async Task<string> LoadDocumentFromUri(IKernel kernel, Uri uri, OpenApiSkillExecutionParameters? executionParameters, HttpClient httpClient, CancellationToken cancellationToken)
    {
        using HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, uri.ToString());
        if (!string.IsNullOrEmpty(executionParameters?.UserAgent))
        {
            requestMessage.Headers.UserAgent.Add(ProductInfoHeaderValue.Parse(executionParameters!.UserAgent));
        }

        using HttpResponseMessage response = await httpClient.SendWithSuccessCheckAsync(requestMessage, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        return await response.Content.ReadAsStringWithExceptionMappingAsync().ConfigureAwait(continueOnCapturedContext: false);
    }


}

/// <summary>
/// Provides extension methods for working with HTTP content in a way that translates HttpRequestExceptions into HttpOperationExceptions.
/// </summary>
internal static class HttpContentExtensions
{
    /// <summary>
    /// Reads the content of the HTTP response as a string and translates any HttpRequestException into an HttpOperationException.
    /// </summary>
    /// <param name="httpContent">The HTTP content to read.</param>
    /// <returns>A string representation of the HTTP content.</returns>
    public static async Task<string> ReadAsStringWithExceptionMappingAsync(this HttpContent httpContent)
    {
        try
        {
            return await httpContent.ReadAsStringAsync().ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new HttpOperationException(message: ex.Message, innerException: ex);
        }
    }

    /// <summary>
    /// Reads the content of the HTTP response as a stream and translates any HttpRequestException into an HttpOperationException.
    /// </summary>
    /// <param name="httpContent">The HTTP content to read.</param>
    /// <returns>A stream representing the HTTP content.</returns>
    public static async Task<Stream> ReadAsStreamAndTranslateExceptionAsync(this HttpContent httpContent)
    {
        try
        {
            return await httpContent.ReadAsStreamAsync().ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new HttpOperationException(message: ex.Message, innerException: ex);
        }
    }

    /// <summary>
    /// Reads the content of the HTTP response as a byte array and translates any HttpRequestException into an HttpOperationException.
    /// </summary>
    /// <param name="httpContent">The HTTP content to read.</param>
    /// <returns>A byte array representing the HTTP content.</returns>
    public static async Task<byte[]> ReadAsByteArrayAndTranslateExceptionAsync(this HttpContent httpContent)
    {
        try
        {
            return await httpContent.ReadAsByteArrayAsync().ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new HttpOperationException(message: ex.Message, innerException: ex);
        }
    }
}


internal static class HttpClientExtensions
{
    /// <summary>
    /// Sends an HTTP request using the provided <see cref="HttpClient"/> instance and checks for a successful response.
    /// If the response is not successful, it logs an error and throws an <see cref="HttpOperationException"/>.
    /// </summary>
    /// <param name="client">The <see cref="HttpClient"/> instance to use for sending the request.</param>
    /// <param name="request">The <see cref="HttpRequestMessage"/> to send.</param>
    /// <param name="completionOption">Indicates if HttpClient operations should be considered completed either as soon as a response is available,
    /// or after reading the entire response message including the content.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> for canceling the request.</param>
    /// <returns>The <see cref="HttpResponseMessage"/> representing the response.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "By design. See comment below.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2016:Forward the 'CancellationToken' parameter to methods", Justification = "The `ReadAsStringAsync` method in the NetStandard 2.0 version does not have an overload that accepts the cancellation token.")]
    internal static async Task<HttpResponseMessage> SendWithSuccessCheckAsync(this HttpClient client, HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken cancellationToken)
    {
        HttpResponseMessage? response = null;

        try
        {
            response = await client.SendAsync(request, completionOption, cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            return response;
        }
        catch (HttpRequestException e)
        {
            string? responseContent = null;

            try
            {
                responseContent = await response!.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            catch { } // We want to suppress any exceptions that occur while reading the content, ensuring that an HttpOperationException is thrown instead.

            throw new HttpOperationException(response!.StatusCode, responseContent, e.Message, e);
        }
    }

    /// <summary>
    /// Sends an HTTP request using the provided <see cref="HttpClient"/> instance and checks for a successful response.
    /// If the response is not successful, it logs an error and throws an <see cref="HttpOperationException"/>.
    /// </summary>
    /// <param name="client">The <see cref="HttpClient"/> instance to use for sending the request.</param>
    /// <param name="request">The <see cref="HttpRequestMessage"/> to send.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> for canceling the request.</param>
    /// <returns>The <see cref="HttpResponseMessage"/> representing the response.</returns>
    internal static async Task<HttpResponseMessage> SendWithSuccessCheckAsync(this HttpClient client, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return await client.SendWithSuccessCheckAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
    }
}


/// <summary>
/// Provides functionality for retrieving instances of HttpClient.
/// </summary>
internal static class HttpClientProvider
{
    /// <summary>
    /// Retrieves an instance of HttpClient.
    /// </summary>
    /// <param name="httpHandlerFactory">The <see cref="IDelegatingHandlerFactory"/> to be used when the HttpClient is not provided already</param>
    /// <param name="httpClient">An optional pre-existing instance of HttpClient.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    /// <returns>An instance of HttpClient.</returns>
    public static HttpClient GetHttpClient(IDelegatingHandlerFactory httpHandlerFactory, HttpClient? httpClient, ILoggerFactory? loggerFactory)
    {
        if (httpClient is null)
        {
            var providedHttpHandler = httpHandlerFactory.Create(loggerFactory);
            providedHttpHandler.InnerHandler = NonDisposableHttpClientHandler.Instance;
            return new HttpClient(providedHttpHandler, false); // We should refrain from disposing the underlying SK default HttpClient handler as it would impact other HTTP clients that utilize the same handler.
        }

        return httpClient;
    }
}


/// <summary>
/// Represents a singleton implementation of <see cref="HttpClientHandler"/> that is not disposable.
/// </summary>
internal sealed class NonDisposableHttpClientHandler : HttpClientHandler
{
    /// <summary>
    /// Private constructor to prevent direct instantiation of the class.
    /// </summary>
    private NonDisposableHttpClientHandler()
    {
        this.CheckCertificateRevocationList = true;
    }

    /// <summary>
    /// Gets the singleton instance of <see cref="NonDisposableHttpClientHandler"/>.
    /// </summary>
    public static NonDisposableHttpClientHandler Instance { get; } = new();

    /// <summary>
    /// Disposes the underlying resources held by the <see cref="NonDisposableHttpClientHandler"/>.
    /// This implementation does nothing to prevent unintended disposal, as it may affect all references.
    /// </summary>
    /// <param name="disposing">True if called from <see cref="Dispose"/>, false if called from a finalizer.</param>
#pragma warning disable CA2215 // Dispose methods should call base class dispose
    protected override void Dispose(bool disposing)
#pragma warning restore CA2215 // Dispose methods should call base class dispose
    {
        // Do nothing if called explicitly from Dispose, as it may unintentionally affect all references.
        // The base.Dispose(disposing) is not called to avoid invoking the disposal of HttpClientHandler resources.
        // This implementation assumes that the HttpClientHandler is being used as a singleton and should not be disposed directly.
    }
}
