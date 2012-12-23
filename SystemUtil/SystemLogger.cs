using RightEdge.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RightEdge.Util
{
    public class SystemLogger
    {
        SystemData _systemData;
        DateTime _systemStartTime { get; set; }

        public SystemLogger(SystemData systemData)
        {
            _systemData = systemData;
            _systemStartTime = DateTime.Now;
        }

        public void Log(OutputSeverityLevel severity, string source, Symbol symbol, string message)
        {
            _systemData.Output.Add(severity, message, symbol, source);

            DateTime currentTime;
            if (_systemData.LiveMode)
            {
                currentTime = DateTime.Now;
            }
            else
            {
                currentTime = _systemData.CurrentDate;
            }

            string systemDirectory = Path.GetDirectoryName(_systemData.TradingSystemProjectPath);
            string logFileName;
            if (_systemData.LiveMode)
            {
                logFileName = Path.Combine(systemDirectory, "LiveActivityLog_" + currentTime.ToString("yyyy'-'MM'-'dd") + ".txt");
            }
            else
            {
                logFileName = Path.Combine(systemDirectory, "SimActivityLog_" + _systemStartTime.ToString("yyyy'-'MM'-'dd' 'HH'-'mm'-'ss") + ".txt");
            }

            string sourceString = string.Empty;
            if (!string.IsNullOrEmpty(source))
            {
                sourceString = " " + source;
            }

            string symbolString = string.Empty;
            if (symbol != null)
            {
                symbolString = " " + symbol.ToString();
            }            

            File.AppendAllText(logFileName, string.Format("{0}{1}{2}: {3}",
                currentTime.ToString("yyyy'-'MM'-'dd' 'HH'-'mm'-'ss"), sourceString, symbolString, message) + Environment.NewLine);
        }
    }
}
