using System;
using System.Linq;

namespace AllFortified {
    /// <summary>
    /// Used to hide 2 chars information in strings (works specifically with nk's font, and with some others)
    /// </summary>
    internal static class MessageHider {
        /// <summary>
        /// Represents a 0 bit with an invisible char
        /// </summary>
        public const char ZeroBit = '​';
        /// <summary>
        /// Represents a 1 bit with an invisible char
        /// </summary>
        public const char OneBit = '­';

        /// <summary>
        /// Converts a 4 char message into a long
        /// </summary>
        /// <param name="message">The message to convert</param>
        /// <returns>The converted long</returns>
        private static long ToCode(string message) => ((long)message[0] << (16 * 3)) | ((long)message[1] << (16 * 2)) | ((long)message[2] << 16) | (long)message[3];

        /// <summary>
        /// Converts a long into a 4 char message
        /// </summary>
        /// <param name="code">The long to convert</param>
        /// <returns>The converted message</returns>
        private static string ToMessage(long code) => new(new char[] { (char)(code >> (16 * 3)), (char)(code >> (16 * 2)), (char)(code >> 16), (char)code });

        /// <summary>
        /// Takes in a 4 character message and 
        /// </summary>
        /// <param name="message">The message to hide</param>
        /// <returns>The message encoded in invisible beinary</returns>
        /// <exception cref="ArgumentException">If the message to hide is not exactly 4 chars long</exception>
        private static string ToInvisibleBinary(string message) {
            if (message.Length != 4)
                throw new ArgumentException("The given message must be exactly 4 chars long.", nameof(message));

            long code = ToCode(message);
            string binary = Convert.ToString(code, 2).PadLeft(64, ZeroBit);
            binary = binary.Replace('0', ZeroBit);
            binary = binary.Replace('1', OneBit);
            return binary;
        }

        /// <summary>
        /// Hides a 4 char message at the end of a string, encrypted using invisible chars
        /// </summary>
        /// <param name="toHideIn">The message to append the hidden message</param>
        /// <param name="message">The message to hide</param>
        /// <returns>A new combined string with the hidden message</returns>
        /// <exception cref="ArgumentException">If the message to hide is not exactly 4 chars long</exception>
        public static string HideMessage(string toHideIn, string message) => toHideIn + ToInvisibleBinary(message);

        /// <summary>
        /// Retrieves all hidden messages found at the end of a string
        /// </summary>
        /// <param name="hiddenIn">The string with hidden messages</param>
        /// <returns>All hidden messages</returns>
        public static string[] RetrieveMessages(string hiddenIn) {
            int start = hiddenIn.Length;
            int length = 0;
            for (int i = hiddenIn.Length - 1; i > -1; i--, length++) {
                if (hiddenIn[i] != ZeroBit && hiddenIn[i] != OneBit) {
                    start = i + 1;
                    break;
                }
            }

            if (length % 64 == 0) {
                string[] messages = Enumerable.Range(0, length / 64).Select(i => hiddenIn.Substring(start + 64 * i, 64)).ToArray();
                for (int i = 0; i < messages.Length; i++) {
                    string binary = messages[i];
                    binary = binary.Replace(ZeroBit, '0');
                    binary = binary.Replace(OneBit, '1');
                    long code = Convert.ToInt64(binary, 2);
                    messages[i] = ToMessage(code);
                }
                return messages;
            } else
                return Array.Empty<string>();
        }

        /// <summary>
        /// Returns if the given string contains the given hidden message
        /// </summary>
        /// <param name="hiddenIn">The string</param>
        /// <param name="message">The hidden message</param>
        /// <returns>True if the string contain the hidden message, false otherwise</returns>
        /// <exception cref="ArgumentException">If the message to hide is not exactly 4 chars long</exception>
        public static bool HasMessage(string hiddenIn, string message) {
            if (message.Length != 4)
                throw new ArgumentException("The given message must be exactly 4 chars long.", nameof(message));

            string[] messages = RetrieveMessages(hiddenIn);
            return messages.Any(m => m.Equals(message));
        }

        /// <summary>
        /// Removes all instances of the hidden message from the string
        /// </summary>
        /// <param name="hiddenIn">The string with the hidden messages</param>
        /// <param name="message">The hidden message</param>
        /// <returns>A string without the hidden message</returns>
        /// <exception cref="ArgumentException">If the message to hide is not exactly 4 chars long</exception>
        public static string RemoveMessage(string hiddenIn, string message) {
            string binary = ToInvisibleBinary(message);
            for (int i = message.Length - 64; i > -1; i -= 64) {
                if (hiddenIn.Substring(i, 64).Equals(binary)) {
                    hiddenIn = hiddenIn.Remove(i, 64);
                    i += 64; // cancel out index change since text was just removed
                }
            }
            return hiddenIn;
        }

        /// <summary>
        /// Removes all hidden information from the message
        /// </summary>
        /// <param name="hiddenIn">The message with hidden information</param>
        /// <returns>A clean message</returns>
        public static string Clean(string hiddenIn) => hiddenIn.Replace($"{ZeroBit}", "").Replace($"{OneBit}", "");
    }
}
