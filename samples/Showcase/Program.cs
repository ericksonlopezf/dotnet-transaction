// Copyright © Erickson Lopez. MIT License.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Transaction.Showcase.Levels;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.Transaction.Showcase;

public static class Program
{
    private static readonly ILevel[] Levels =
    [
        new Level0_Conceptual(),
        new Level1_QuickStart(),
        new Level2_Configuration(),
        new Level3_RealUseCases(),
        new Level4_AdvancedIntegration(),
        new Level5_Processing(),
        new Level6_ErrorHandling(),
        new Level7_Scalability(),
        new Level8_Customization(),
        new Level9_Extensions(),
        new Level10_EnterpriseArchitecture()
    ];

    public static async Task<int> Main(string[] args)
    {
        var services = new ServiceCollection();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        if (args.Length > 0)
        {
            if (args.Contains("--all", StringComparer.OrdinalIgnoreCase))
            {
                return await RunAllLevelsAsync(serviceProvider);
            }

            int levelIndex = Array.IndexOf(args, "--level");
            if (levelIndex >= 0 && levelIndex + 1 < args.Length && int.TryParse(args[levelIndex + 1], out int targetLevel))
            {
                ILevel? selectedLevel = Levels.FirstOrDefault(l => l.LevelNumber == targetLevel);
                if (selectedLevel is not null)
                {
                    await selectedLevel.RunAsync(serviceProvider, CancellationToken.None);
                    return 0;
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: Level {targetLevel} not found. Valid levels are 0 to {Levels.Length - 1}.");
                Console.ResetColor();
                return 1;
            }
        }

        return await RunInteractiveMenuAsync(serviceProvider);
    }

    private static async Task<int> RunAllLevelsAsync(IServiceProvider serviceProvider)
    {
        PrintBanner();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Executing all {Levels.Length} Showcase Levels in batch mode...\n");
        Console.ResetColor();

        int passed = 0;
        foreach (ILevel level in Levels)
        {
            try
            {
                await level.RunAsync(serviceProvider, CancellationToken.None);
                passed++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[ERROR in Level {level.LevelNumber:D2}: {level.Name}]");
                Console.WriteLine(ex.ToString());
                Console.ResetColor();
                return 1;
            }
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("================================================================================");
        Console.WriteLine($"  SHOWCASE AUDIT COMPLETE: {passed}/{Levels.Length} LEVELS EXECUTED SUCCESSFULLY (100%)");
        Console.WriteLine("================================================================================\n");
        Console.ResetColor();

        return 0;
    }

    private static async Task<int> RunInteractiveMenuAsync(IServiceProvider serviceProvider)
    {
        while (true)
        {
            PrintBanner();
            Console.WriteLine("Available Progressive Levels:\n");

            foreach (ILevel level in Levels)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($" [{level.LevelNumber,2}] ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"{level.Name,-55} ");
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"({level.Category})");
            }

            Console.ResetColor();
            Console.WriteLine("\n [A]  Run All Levels Sequentially");
            Console.WriteLine(" [Q]  Quit Showcase\n");

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("Select an option (0-10, A, Q): ");
            Console.ResetColor();

            string? input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input))
            {
                continue;
            }

            if (input.Equals("Q", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Exiting Showcase.");
                return 0;
            }

            if (input.Equals("A", StringComparison.OrdinalIgnoreCase))
            {
                int exitCode = await RunAllLevelsAsync(serviceProvider);
                if (exitCode != 0) return exitCode;
                Console.WriteLine("Press Enter to return to menu...");
                Console.ReadLine();
                continue;
            }

            if (int.TryParse(input, out int selectedNumber))
            {
                ILevel? targetLevel = Levels.FirstOrDefault(l => l.LevelNumber == selectedNumber);
                if (targetLevel is not null)
                {
                    SafeClear();
                    await targetLevel.RunAsync(serviceProvider, CancellationToken.None);
                    Console.WriteLine("Press Enter to return to menu...");
                    Console.ReadLine();
                    continue;
                }
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Invalid option. Please enter a valid level number, 'A', or 'Q'.");
            Console.ResetColor();
            await Task.Delay(1000);
        }
    }

    private static void PrintBanner()
    {
        SafeClear();
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine("""
================================================================================
  EricksonLopez.Transaction — Official Showcase & Executable Reference
  Version 1.0.0 | .NET 10.0 | C# 14 | Native AOT Ready
================================================================================
""");
        Console.ResetColor();
    }

    private static void SafeClear()
    {
        try
        {
            if (!Console.IsOutputRedirected && !Console.IsInputRedirected)
            {
                Console.Clear();
            }
        }
        catch
        {
            // Ignore console redirect handle errors in non-interactive sessions
        }
    }
}
