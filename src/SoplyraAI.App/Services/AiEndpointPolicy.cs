namespace SoplyraAI.Services;

public static class AiEndpointPolicy
{
    public static bool TryValidate(string? endpoint, bool allowRemote, out Uri? uri, out string error)
    {
        uri = null;
        error = "";

        var value = (endpoint ?? "").Trim();
        if (value.Length == 0 || value.Length > 2048 ||
            !Uri.TryCreate(value, UriKind.Absolute, out var parsed))
        {
            error = "Enter a valid absolute AI endpoint.";
            return false;
        }

        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
        {
            error = "AI endpoints must use HTTP or HTTPS.";
            return false;
        }

        if (!string.IsNullOrEmpty(parsed.UserInfo) ||
            !string.IsNullOrEmpty(parsed.Query) ||
            !string.IsNullOrEmpty(parsed.Fragment))
        {
            error = "AI endpoints cannot contain credentials, query strings, or fragments.";
            return false;
        }

        if (parsed.IsLoopback)
        {
            uri = parsed;
            return true;
        }

        if (!allowRemote)
        {
            error = "Remote AI endpoints are disabled. Enable the explicit remote-AI opt-in to use one.";
            return false;
        }

        if (parsed.Scheme != Uri.UriSchemeHttps)
        {
            error = "Remote AI endpoints must use HTTPS.";
            return false;
        }

        uri = parsed;
        return true;
    }

    public static Uri BuildChatCompletionsUri(Uri baseUri) =>
        new(baseUri.AbsoluteUri.TrimEnd('/') + "/chat/completions");
}
