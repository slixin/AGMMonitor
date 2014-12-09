using AutomationHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;

namespace PWDEncryptor
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Format: PWDEncryptor.exe <password>, Example: PWDEncryptor.exe abc123");
            if (args.Length == 0)
                return;
            string pwd = args[0];

            Console.WriteLine(Encryption.Encrypt(pwd, GetProcessorSerial()));
            Console.ReadLine();
        }

        static string GetProcessorSerial()
        {
            string cpuInfo = string.Empty;
            ManagementClass cimobject = new ManagementClass("Win32_Processor");
            ManagementObjectCollection moc = cimobject.GetInstances();
            foreach (ManagementObject mo in moc)
            {
                cpuInfo = mo.Properties["ProcessorId"].Value.ToString();
            }

            return cpuInfo;
        }
    }
}
