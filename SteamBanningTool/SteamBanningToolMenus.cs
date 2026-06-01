using Console = System.Console;

namespace SteamBanningTool.App
{
    public static class SteamBanningToolMenus
    {
        private static int menuIndex = 0;

        public static async Task RunMenu()
        {
            while(menuIndex != -1)
            {
                switch(menuIndex)
                {
                    case 0:
                        menuIndex = ShowMenu();
                        break;
                    case 1:
                        menuIndex = await ReportProfileMenu();
                        break;
                    case 2:
                        menuIndex = await BanProfileMenu();
                        break;
                    case 3:
                        menuIndex = await UnbanProfileMenu();
                        break;
                    case 4:
                        menuIndex = await ReportAndBanProfileMenu();
                        break;
                    case 5:
                        menuIndex = await ListCheatingReportsMenu();
                        break;
                    case 6:
                        menuIndex = ChangeSettingsMenu();
                        break;
                    default:
                        Console.WriteLine("Invalid option. Returning to main menu...");
                        menuIndex = ShowMenu();
                        break;
                }
            }

            Console.Clear();
        }

        #region Menu Methods
        /// Menu index: 0
        public static int ShowMenu(bool ShowInvalidOptionMessage = false)
        {
            SetTitle("Steam Banning Tool");
            Console.WriteLine("1. Report profile");
            Console.WriteLine("2. Ban profile");
            Console.WriteLine("3. Unban profile");
            Console.WriteLine("4. Report and ban profile");
            Console.WriteLine("5. List app bans/reports");
            Console.WriteLine("6. Change settings");
            Console.WriteLine("7. Exit");

            if(ShowInvalidOptionMessage)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("\nInvalid option. Please select a valid option.");
                Console.ForegroundColor = ConsoleColor.White;
            }

            Console.WriteLine();
            Console.Write("Select an option: ");
            int.TryParse(Console.ReadLine(), out int selectedOption);

            if(selectedOption < 1 || selectedOption > 7)
            {
                return ShowMenu(true);
            }
            else if(selectedOption == 7)
            {
                selectedOption = -1;
            }
            else if(!AppSettings.IsConfigured() && selectedOption >= 1 && selectedOption <= 5)
            {
                selectedOption = 6;
            }

            return selectedOption;
        }

        /// Menu index: 1
        public static async Task<int> ReportProfileMenu()
        {
            if(!RequireConfigured())
            {
                return 6;
            }

            SetTitle("Report profile");
            Console.WriteLine("You will use the saved Steam Partner key and App ID from settings.");

            string steamId64 = ReadRequiredValue("Enter the SteamID64 of the user to report: ");

            var reportResult = await SteamBanApiClient.ReportPlayerAsync(AppSettings.PartnerKey, AppSettings.AppID, steamId64);

            if(reportResult.Success)
            {
                string? reportId = SteamBanApiClient.TryGetJsonValue(reportResult.ResponseText, "response", "reportid");
                if(!string.IsNullOrEmpty(reportId))
                {
                    Console.WriteLine("Report ID request successful.");
                    Console.WriteLine($"The Report ID for this session is: {reportId}");
                }
                else
                {
                    Console.WriteLine("Report request succeeded, but the Report ID was not found in the response.");
                }
            }
            else
            {
                Console.WriteLine($"There was an error in your report request. {reportResult.ErrorMessage}");
            }

            ReturnToMenuPrompt();
            return 0;
        }

        /// Menu index: 2
        public static async Task<int> BanProfileMenu()
        {
            if(!RequireConfigured())
            {
                return 6;
            }

            SetTitle("Ban profile");
            Console.WriteLine("Make sure you have already reported this profile before banning it.");

            string confirmReport = ReadRequiredValue("Have you already reported this profile? Y/N: ").ToUpperInvariant();
            if(confirmReport != "Y")
            {
                Console.WriteLine("You need to report the user first. Returning to the main menu...");
                ReturnToMenuPrompt();
                return 0;
            }

            string steamId64 = ReadRequiredValue("Enter the SteamID64 of the user to ban: ");
            string reportId = ReadRequiredValue("Enter the ReportID received from the report: ");
            string cheatDescription = ReadRequiredValue("Enter a reason for the ban: ");
            string duration = ReadRequiredValue("Enter the duration in seconds (0 = permanent): ");

            var banResult = await SteamBanApiClient.BanPlayerAsync(AppSettings.PartnerKey, AppSettings.AppID, steamId64, reportId, cheatDescription, duration);

            if(banResult.Success)
            {
                Console.WriteLine("Your ban request was successful!");
            }
            else
            {
                Console.WriteLine($"There was an error in your request. {banResult.ErrorMessage}");
            }

            ReturnToMenuPrompt();
            return 0;
        }

        /// Menu index: 3
        public static async Task<int> UnbanProfileMenu()
        {
            if(!RequireConfigured())
            {
                return 6;
            }

            SetTitle("Unban profile");
            Console.WriteLine("You will use the saved Steam Partner key and App ID from settings.");

            string steamId64 = ReadRequiredValue("Enter the SteamID64 of the user to unban: ");

            var unbanResult = await SteamBanApiClient.UnbanPlayerAsync(AppSettings.PartnerKey, AppSettings.AppID, steamId64);

            if(unbanResult.Success)
            {
                Console.WriteLine("Your unban request was successful!");
            }
            else
            {
                Console.WriteLine($"There was an error in your request. {unbanResult.ErrorMessage}");
            }

            ReturnToMenuPrompt();
            return 0;
        }

        /// Menu index: 4
        public static async Task<int> ReportAndBanProfileMenu()
        {
            if(!RequireConfigured())
            {
                return 6;
            }

            SetTitle("Report and ban profile");
            Console.WriteLine("This will report the user first, then apply the ban using the returned Report ID.");

            string steamId64 = ReadRequiredValue("Enter the SteamID64 of the user: ");
            string cheatDescription = ReadRequiredValue("Enter a reason for the ban: ");
            string duration = ReadRequiredValue("Enter the duration in seconds (0 = permanent): ");

            var reportResult = await SteamBanApiClient.ReportPlayerAsync(AppSettings.PartnerKey, AppSettings.AppID, steamId64);

            if(!reportResult.Success)
            {
                Console.WriteLine($"Something went wrong while creating the report. {reportResult.ErrorMessage}");
                ReturnToMenuPrompt();
                return 0;
            }

            string? reportId = SteamBanApiClient.TryGetJsonValue(reportResult.ResponseText, "response", "reportid");
            if(string.IsNullOrEmpty(reportId))
            {
                Console.WriteLine("The report succeeded, but the Report ID could not be read from the response.");
                ReturnToMenuPrompt();
                return 0;
            }

            Console.WriteLine("Report ID request successful.");
            Console.WriteLine($"The Report ID for this session is: {reportId}");

            var banResult = await SteamBanApiClient.BanPlayerAsync(AppSettings.PartnerKey, AppSettings.AppID, steamId64, reportId, cheatDescription, duration);

            if(banResult.Success)
            {
                Console.WriteLine("Your ban request was successful!");
            }
            else
            {
                Console.WriteLine($"There was an error in your ban request. {banResult.ErrorMessage}");
            }

            ReturnToMenuPrompt();
            return 0;
        }

        /// Menu index: 5
        public static async Task<int> ListCheatingReportsMenu()
        {
            if(!RequireConfigured())
            {
                return 6;
            }

            SetTitle("List app bans/reports");
            Console.WriteLine("This uses GetCheatingReports with includebans=true.");

            string? steamIdInput = ReadOptionalValue("Enter a SteamID64 to filter by, or press Enter for all: ");
            string? timeBeginInput = ReadOptionalValue("Enter start time (Unix seconds) or press Enter for 0: ");
            string? timeEndInput = ReadOptionalValue("Enter end time (Unix seconds) or press Enter for now: ");

            uint timeBegin = ParseUInt32OrDefault(timeBeginInput, 0);
            uint timeEnd = ParseUInt32OrDefault(timeEndInput, (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            SetTitle("List app bans/reports");
            Console.WriteLine($"Fetching reports...");


            var listResult = await SteamBanApiClient.GetCheatingReportsAsync(
                AppSettings.PartnerKey,
                AppSettings.AppID,
                timeBegin,
                timeEnd,
                0,
                includeReports: false,
                includeBans: true,
                string.IsNullOrWhiteSpace(steamIdInput) ? null : steamIdInput);

            SetTitle("List app bans/reports");
            if(listResult.Success)
            {
                Console.WriteLine("\nQuery successful. Response:");
                Console.WriteLine(SteamBanApiClient.FormatCheatingReportsTable(listResult.ResponseText));
            }
            else
            {
                Console.WriteLine($"There was an error while listing bans/reports. {listResult.ErrorMessage}");
            }

            ReturnToMenuPrompt();
            return 0;
        }

        /// Menu index: 6
        public static int ChangeSettingsMenu()
        {
            SetTitle("Change Settings");
            Console.WriteLine("Set your Steam Partner key, it must be a key for everyone in your organiztion.");

            string partnerKey = string.Empty;
            while(string.IsNullOrEmpty(partnerKey))
            {
                Console.Write("Enter your Steam Partner key: ");
                partnerKey = Console.ReadLine() ?? string.Empty;

                if(string.IsNullOrEmpty(partnerKey))
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine("Partner key cannot be empty. Please enter a valid key.");
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }

            Console.WriteLine("\nSet the App ID of the game you want to manage bans for.");

            ulong appId = 0;
            while(appId == 0)
            {
                Console.Write("Enter your App ID: ");
                ulong.TryParse(Console.ReadLine(), out appId);

                if(appId == 0)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine("App ID must be a valid number. Please enter a valid App ID.");
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }

            AppSettings.PartnerKey = partnerKey;
            AppSettings.AppID = appId;

            Console.WriteLine("\nSettings updated successfully. Press any key to return to the main menu...");
            Console.ReadKey();

            return 0;
        }
        #endregion

        private static void SetTitle(string title)
        {
            Console.Clear();
            Console.WriteLine(title);
            Console.WriteLine(new string('=', title.Length));
        }

        private static bool RequireConfigured()
        {
            if(AppSettings.IsConfigured())
            {
                return true;
            }

            Console.WriteLine("You must configure a Steam Partner key and App ID before using this option.");
            ReturnToMenuPrompt();
            return false;
        }

        private static string ReadRequiredValue(string prompt)
        {
            string value = string.Empty;
            while(string.IsNullOrWhiteSpace(value))
            {
                Console.Write(prompt);
                value = (Console.ReadLine() ?? string.Empty).Trim();

                if(string.IsNullOrWhiteSpace(value))
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine("Value cannot be empty. Please enter a valid value.");
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }

            return value;
        }

        private static string? ReadOptionalValue(string prompt)
        {
            Console.Write(prompt);
            string value = (Console.ReadLine() ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static uint ParseUInt32OrDefault(string? value, uint defaultValue)
        {
            return uint.TryParse(value, out uint parsedValue) ? parsedValue : defaultValue;
        }

        private static void ReturnToMenuPrompt()
        {
            Console.WriteLine("\nPress any key to return to the main menu.");
            Console.ReadKey(true);
        }
    }
}