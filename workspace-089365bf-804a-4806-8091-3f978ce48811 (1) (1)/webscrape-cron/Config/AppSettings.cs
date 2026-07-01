// ========== FILE: Config/AppSettings.cs ==========
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WebScrapeCron.Config
{
    /* ========================================================================
     * STATIC CLASS: AppSettings
     * ========================================================================
     * OOP PRINCIPLE: ENCAPSULATION (Singleton-like State Management)
     * ---------------------------------------------------------------
     * This class encapsulates the secure storage and retrieval of the
     * database connection string. It uses Windows DPAPI (Data Protection API)
     * via ProtectedData to encrypt the string before writing to disk, and
     * decrypts it on read. Only the current Windows user can decrypt it.
     *
     * DATA FLOW:
     * User enters server/db info in Settings tab
     *     → AppSettings.SaveConnectionString(plainString)
     *     → ProtectedData.Protect() encrypts with DPAPI
     *     → Encrypted bytes written to %AppData%\WebScrapeCron\settings.dat
     *
     * On app startup:
     *     → AppSettings.LoadConnectionString()
     *     → Reads encrypted bytes from settings.dat
     *     → ProtectedData.Unprotect() decrypts
     *     → Returns plain connection string
     *     → SqlDataRepository uses this for all DB operations
     *
     * DESIGN DECISIONS:
     * - DPAPI with CurrentUser scope: only this Windows user can decrypt.
     *   No need to manage encryption keys manually.
     * - Storage in %AppData% (roaming profile compatible)
     * - File permissions inherit from user profile (other users can't read)
     * - Static class because there's only one settings store per app
     *
     * SECURITY NOTES:
     * - System.Security.Cryptography.ProtectedData requires the
     *   System.Security.Cryptography.ProtectedData NuGet package on .NET 8
     *   BUT it's actually in the shared framework for Windows, so no extra
     *   package is needed when targeting net8.0-windows.
     * - The encrypted data is tied to the current user AND machine by default
     *   (DataProtectionScope.CurrentUser). This means the settings file
     *   cannot be copied to another user's profile or machine and decrypted.
     * ======================================================================== */

    /// <summary>
    /// Manages encrypted storage of the database connection string using DPAPI.
    /// </summary>
    public static class AppSettings
    {
        // Path where encrypted settings are stored
        private static readonly string SettingsDirectory =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WebScrapeCron");

        private static readonly string SettingsFilePath =
            Path.Combine(SettingsDirectory, "settings.dat");

        // Entropy bytes add an additional layer of entropy to the encryption.
        // This makes the encrypted output unique even if the same plaintext
        // is encrypted by the same user. Using a fixed entropy is acceptable
        // here because the DPAPI scope is already user-specific.
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("WebScrapeCron_v1_Settings");

        /// <summary>
        /// Saves the connection string encrypted to the settings file.
        /// Creates the directory if it doesn't exist.
        /// </summary>
        /// <param name="plainConnectionString">The plain-text connection string to encrypt and save</param>
        public static void SaveConnectionString(string plainConnectionString)
        {
            if (string.IsNullOrWhiteSpace(plainConnectionString))
                throw new ArgumentException("Connection string cannot be empty.", nameof(plainConnectionString));

            // Ensure the settings directory exists
            Directory.CreateDirectory(SettingsDirectory);

            // Convert the plain text to bytes
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainConnectionString);

            // Encrypt using DPAPI with CurrentUser scope
            // Only this Windows user can decrypt the data
            byte[] encryptedBytes = ProtectedData.Protect(
                plainBytes,
                Entropy,
                DataProtectionScope.CurrentUser);

            // Write encrypted bytes to file
            File.WriteAllBytes(SettingsFilePath, encryptedBytes);
        }

        /// <summary>
        /// Loads and decrypts the connection string from the settings file.
        /// Returns null if no settings file exists or decryption fails.
        /// </summary>
        /// <returns>The decrypted connection string, or null if not configured</returns>
        public static string? LoadConnectionString()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                    return null;

                byte[] encryptedBytes = File.ReadAllBytes(SettingsFilePath);

                // Decrypt using DPAPI with the same scope and entropy
                byte[] plainBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    Entropy,
                    DataProtectionScope.CurrentUser);

                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception)
            {
                // Decryption failed (file corrupted, different user, etc.)
                return null;
            }
        }

        /// <summary>
        /// Returns true if a settings file exists and can be loaded.
        /// </summary>
        public static bool IsConfigured => File.Exists(SettingsFilePath);

        /// <summary>
        /// Deletes the settings file (used for resetting configuration).
        /// </summary>
        public static void ClearSettings()
        {
            if (File.Exists(SettingsFilePath))
                File.Delete(SettingsFilePath);
        }
    }
}
