using System;
using C = System.Console;

namespace XeytanCSharpServer.Ui.Console.Views
{
    class ShellView : ClientView
    {
        protected override string GetBannerLabel()
        {
            return "Shell";
        }

        public override string Loop()
        {
            while (true)
            {
                PrintBanner();
                string instruction = System.Console.ReadLine();
                if (instruction == null) continue;
                if (!ProcessInstruction(instruction))
                    return instruction;
            }
        }

        private bool ProcessInstruction(string instruction)
        {
            string[] parts = instruction.Split();
            if (string.IsNullOrEmpty(parts[0].Trim()))
                return true;

            if (parts.Length > 1)
            {
                if (parts[0].Equals("help", StringComparison.OrdinalIgnoreCase))
                {
                    // TODO: print help message
                    return true;
                }
            }

            return false;
        }

        public void PrintOutput(string command, string output)
        {
            C.Write(output);
        }
    }
}