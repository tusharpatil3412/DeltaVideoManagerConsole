using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AddTimeStampConsole
{
    public class Constants
    {
        public static readonly string InputVideoPath;
        public static readonly string OutputVideoPath;

        static Constants()
        {
            // Initialize ConfigurationBuilder
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();


            InputVideoPath = config["VideoPaths:InputVideoPath"]!;
            OutputVideoPath = config["VideoPaths:OutputVideoPath"]!;

        }
    }
}
