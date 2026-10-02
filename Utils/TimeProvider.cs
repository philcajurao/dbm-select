using System;

namespace dbm_select.Utils
{
    /// <summary>
    /// Provides centralized access to the current date and time, with an optional testing override.
    /// Use this to test time-dependent logic (like Excel log filenames and SQLite timestamps)
    /// without changing dates across multiple files.
    /// </summary>
    public static class TimeProvider
    {
        /// <summary>
        /// SET THIS FOR TESTING: Set to a specific date to override "Today" and "Now" globally.
        /// Keep as null for production (uses actual system date/time).
        /// Example: public static DateTime? TestDateOverride { get; set; } = new DateTime(2026, 6, 15);
        /// </summary>
        public static DateTime? TestDateOverride { get; set; } = null;

        /// <summary>
        /// Gets the current date, using the test override if set.
        /// </summary>
        public static DateTime Today => TestDateOverride?.Date ?? DateTime.Today;

        /// <summary>
        /// Gets the current date and time, using the test override if set (preserving the current time of day).
        /// </summary>
        public static DateTime Now
        {
            get
            {
                if (TestDateOverride.HasValue)
                {
                    // Return the overridden date combined with the current time of day
                    return TestDateOverride.Value.Date.Add(DateTime.Now.TimeOfDay);
                }
                return DateTime.Now;
            }
        }
    }
}
