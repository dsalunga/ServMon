using System;
using System.Linq;

namespace WCMS.Common.Utilities
{
    public static class FtpHelper
    {
        public static string UrlEncode(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            if (!Uri.TryCreate(path, UriKind.Absolute, out var uri))
            {
                return path;
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeFtp, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            var encodedSegments = uri.AbsolutePath
                .Split('/', StringSplitOptions.None)
                .Select(segment => Uri.EscapeDataString(Uri.UnescapeDataString(segment)));
            var encodedPath = string.Join("/", encodedSegments);

            var builder = new UriBuilder(uri)
            {
                Path = encodedPath
            };

            return builder.Uri.AbsoluteUri;
        }
    }
}
