﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using OpenAsset.RestClient.Library;
using OpenAsset.RestClient.Library.Noun;
using OpenAsset.RestClient;

namespace OpenAsset.RestClient.TestLibrary
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            string oaURL = "http://192.168.1.142";
            string username = "axomic";
            string password = "***REMOVED***";
            
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            System.Console.WriteLine("TEST RUNNING!");
            ConnectionFactory.Instance.testMethod();

            ConnectionHelper connectionHelper = ConnectionFactory.Instance.getConnectionHelper(oaURL, username, password);
            try
            {
                bool validUser = connectionHelper.ValidateCredentials();
                File size = connectionHelper.GetObject<File>(957, new RESTOptions());
            }
            catch (RESTAPIException e)
            {
                System.Console.WriteLine(e);
                System.Console.WriteLine("Exception in the test program: \n\t" + e.ErrorObj);
            }
        }
    }
}
