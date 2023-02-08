﻿using csharp_Protoshift.GameSession;
using csharp_Protoshift.resLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using YSFreedom.Common.Net;
using YSFreedom.Common.Util;

namespace csharp_Protoshift.Commands
{
    internal static class CommandLine
    {
        /// <summary>
        /// Add commands here.
        /// </summary>
        static CommandLine()
        {
            handlers.Add(new MT19937Cmd());
            handlers.Add(new SetVerboseCmd());
            handlers.Add(new SelectRecordCmd());
            handlers.Add(new ShowRecordCmd());
        }

        public static List<ICommandHandler> handlers = new();
        public static void ShowHelps()
        { 
            foreach (var handler in handlers)
            {
                Log.Info($"Command '{handler.CommandName}': {handler.Description}", "CommandLine");
                string[] help = handler.Usage.Split(Environment.NewLine);
                foreach (var line in help) Log.Info(line, "CommandLine");
                Log.Info("", "CommandLine");
            }
        }

        private static void RefuseCommand(string commandName)
        {
            Log.Info($"Invalid command: {commandName}.");
        }

        public static async Task Start()
        {
            while (true)
            {
                Console.Write("> ");
                string? cmd = Console.ReadLine();
                if (cmd == null) continue;
                int sepindex = cmd.IndexOf(' ');
                if (sepindex == -1) sepindex = cmd.Length;
                string commandName = cmd.Substring(0, sepindex);
                if (commandName.ToLower() == "help" || commandName == "?")
                {
                    ShowHelps();
                    continue;
                }
                else
                {
                    string[] args = cmd.Substring(Math.Min(cmd.Length, sepindex + 1)).Split(' ');
                    bool handled = false;
                    foreach (var cmdhandle in handlers)
                    {
                        if (cmdhandle.CommandName == commandName)
                        {
                            handled = true;
                            try
                            {
                                await cmdhandle.HandleAsync(args);
                            }
                            catch (Exception ex)
                            {
                                Log.Erro(ex.ToString(), "CommandLine");
                                Log.Info($"Encountered error when handling command {commandName}. Please check your input.", "CommandLine");
                            }
                            break;
                        }
                    }
                    if (!handled) RefuseCommand(commandName);
                }
            }
        }
    }
}