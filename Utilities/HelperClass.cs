using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static IntuneTools.Utilities.Variables;

namespace IntuneTools.Utilities
{
    public class HelperClass
    {
        public static void CreateLogFile()
        {
            // This method will be used to create the log file if it does not exist

            // Check if the log file directory exists
            if (!Directory.Exists(appDataFolder))
            {
                // If the directory does not exist, create it
                Directory.CreateDirectory(appDataFolder);
            }

            // Create a new log file with date and time appended to the name

            string timestamp = DateTime.Now.ToString("HH-mm-ss_dd-MM-yyyy");



            File.Create(appDataFolder + "\\" + timestamp + "_" + logFileName).Close();
        }

        public static void WriteToLog(string data)
        {

            // This method will be used to log data to the main log file


            // Read the last ten lines from the log file
            List<string> lastTenLines = ReadLastLines(primaryLogFile, 5);

            // Check if any of the last ten lines are identical to the new data
            if (lastTenLines.Any(line => line.Contains(data)))
            {
                // If duplicate found, you may choose to handle it as needed
                //MessageBox.Show($"Duplicate entry: {data}");
                return; // Optionally, you can exit the method to avoid writing the duplicate
            }



            // Use the using statement to ensure proper disposal of StreamWriter
            using (StreamWriter sw = new StreamWriter(primaryLogFile, true))
            {
                // Write the data to the log file
                sw.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {data}");
            }
            // StreamWriter is automatically closed and disposed of when leaving the using block

        }

        public static List<string> ReadLastLines(string filePath, int lineCount)
        {
            List<string> lastLines = new List<string>();

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader reader = new StreamReader(fs))
            {
                // Read all lines from the file and keep the last N lines
                LinkedList<string> lines = new LinkedList<string>();
                while (!reader.EndOfStream)
                {
                    lines.AddLast(reader.ReadLine());
                    if (lines.Count > lineCount)
                    {
                        lines.RemoveFirst();
                    }
                }

                lastLines.AddRange(lines);
            }

            return lastLines;
        }
    }
}
