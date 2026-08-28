using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace BroforceOnlineDiagnostics
{
    internal static class OnlinePlayerListFormatter
    {
        internal const float RefreshSeconds = 0.1f;
        private const float HostGradientCycleSeconds = 4f;
        private const string SeparatorRichText = "<color=#767B81> | </color>";
        private static readonly int[,] HostGradientRgb =
        {
            { 255, 226, 122 },
            { 255, 157, 77 },
            { 244, 92, 165 },
            { 155, 123, 255 },
            { 85, 214, 208 },
            { 255, 226, 122 }
        };

        internal static string FormatLatency(string playerName, int latencyMilliseconds)
        {
            playerName = string.IsNullOrEmpty(playerName) ? "Online Player" : playerName;
            var latencyText = latencyMilliseconds < 0
                ? "--ms"
                : latencyMilliseconds.ToString(CultureInfo.InvariantCulture) + "ms";
            var latencyColor = latencyMilliseconds < 0
                ? "#AEB3B8"
                : (latencyMilliseconds <= 80
                    ? "#65D47E"
                    : (latencyMilliseconds <= 150 ? "#F0C85A" : "#EF6B68"));
            return "<color=" + latencyColor + ">" + latencyText + "</color>" +
                   SeparatorRichText +
                   "<color=#F4F4F4>" + EscapeRichText(playerName) + "</color>";
        }

        internal static string FormatHost(string playerName)
        {
            playerName = string.IsNullOrEmpty(playerName) ? "Online Host" : playerName;
            var textElements = new List<string>();
            var enumerator = StringInfo.GetTextElementEnumerator(playerName);
            while (enumerator.MoveNext())
            {
                textElements.Add(enumerator.GetTextElement());
            }

            var phase = (Time.unscaledTime % HostGradientCycleSeconds) /
                        HostGradientCycleSeconds;
            var builder = new StringBuilder(playerName.Length * 24);
            builder.Append("<color=#AEB3B8>HOST</color>");
            builder.Append(SeparatorRichText);
            for (var index = 0; index < textElements.Count; index++)
            {
                var position = textElements.Count <= 1
                    ? phase
                    : (phase + (index / (float)(textElements.Count - 1)) * 0.45f) % 1f;
                builder.Append("<color=#");
                builder.Append(SampleHostGradient(position));
                builder.Append(">");
                builder.Append(EscapeRichText(textElements[index]));
                builder.Append("</color>");
            }
            return builder.ToString();
        }

        internal static int SecondsToMilliseconds(float latencySeconds)
        {
            if (latencySeconds <= 0f || Single.IsNaN(latencySeconds) ||
                Single.IsInfinity(latencySeconds))
            {
                return -1;
            }

            var milliseconds = (int)global::System.Math.Round(latencySeconds * 1000f);
            return global::System.Math.Max(1, global::System.Math.Min(9999, milliseconds));
        }

        private static string SampleHostGradient(float position)
        {
            position = position - (float)global::System.Math.Floor(position);
            var segmentCount = HostGradientRgb.GetLength(0) - 1;
            var scaledPosition = position * segmentCount;
            var segment = global::System.Math.Min(
                segmentCount - 1,
                (int)global::System.Math.Floor(scaledPosition));
            var amount = scaledPosition - segment;
            var red = InterpolateColor(
                HostGradientRgb[segment, 0],
                HostGradientRgb[segment + 1, 0],
                amount);
            var green = InterpolateColor(
                HostGradientRgb[segment, 1],
                HostGradientRgb[segment + 1, 1],
                amount);
            var blue = InterpolateColor(
                HostGradientRgb[segment, 2],
                HostGradientRgb[segment + 1, 2],
                amount);
            return red.ToString("X2", CultureInfo.InvariantCulture) +
                   green.ToString("X2", CultureInfo.InvariantCulture) +
                   blue.ToString("X2", CultureInfo.InvariantCulture);
        }

        private static int InterpolateColor(int from, int to, float amount)
        {
            return (int)global::System.Math.Round(from + (to - from) * amount);
        }

        private static string EscapeRichText(string value)
        {
            return (value ?? string.Empty).Replace("<", "[").Replace(">", "]");
        }
    }
}
