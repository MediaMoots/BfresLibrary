using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BfresLibrary
{
    /// <summary>
    /// A cache of strings that are saved through raw IDs in place of the pointer field of a name offset.
    /// This is used and required for TOTK models.
    /// </summary>
    public class StringCache
    {
        public static Dictionary<long, string> Strings = new Dictionary<long, string>();

        [ThreadStatic]
        private static ReadValidationScope readValidation;

        /// <summary>
        /// Observes each nonzero Switch string reference before it is returned or sought.
        /// The callback receives the reference, stream length and resolved external string
        /// (null when no external string is available), and may throw to reject the read.
        /// </summary>
        /// <remarks>
        /// Use synchronously on one thread with using. Scopes cannot nest or cross await.
        /// This does not synchronize the process-wide Strings dictionary or other loader state;
        /// callers must still serialize changes to shared parser state.
        /// </remarks>
        public static IDisposable BeginReadValidation(Action<long, long, string> validate)
        {
            if (validate == null) throw new ArgumentNullException(nameof(validate));
            if (readValidation != null) throw new InvalidOperationException("String read validation cannot be nested.");
            return readValidation = new ReadValidationScope(validate);
        }

        internal static bool TryGetExternalString(long reference, long streamLength, out string value)
        {
            bool found = Strings.TryGetValue(reference, out value);
            // Validate the same resolved value the loader will return, without a second lookup.
            readValidation?.Validate(reference, streamLength, value);
            return found;
        }

        private sealed class ReadValidationScope : IDisposable
        {
            internal readonly Action<long, long, string> Validate;
            private readonly int thread = Environment.CurrentManagedThreadId;
            private bool disposed;
            internal ReadValidationScope(Action<long, long, string> validate) { Validate = validate; }
            public void Dispose()
            {
                if (thread != Environment.CurrentManagedThreadId)
                    throw new InvalidOperationException("String read validation must end on its owning thread.");
                if (disposed) return;
                if (readValidation != this)
                    throw new InvalidOperationException("String read validation scope is no longer active.");
                readValidation = null;
                disposed = true;
            }
        }
    }
}
