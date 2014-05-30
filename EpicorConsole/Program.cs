using System;
using System.Collections.Generic;
using System.IO;
using EpicorConsole.Utils;

namespace EpicorConsole
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            switch (args.Length)
            {
                case 1:
                {
                    var serializedData = new StreamReader(File.OpenRead(args[0])).ReadToEnd();
                    var serializer = new TreeSerializer<NavigatorNode>();
                    var tree = serializer.Deserialize(serializedData);
                    Console.WriteLine(serializer.PrettyPrint(tree));
                }
                    break;
                case 2:
                {
                    var charges = new Epicor().GetCurrentCharges(DateTime.Today);
                    foreach (var charge in charges)
                    {
                        Console.WriteLine(charge);
                    }
                }
                    break;
                default:
                {
                    var epicor = new Epicor();
                    var rootActivity = epicor.GetSiteActivities();
                    var json = new TreeSerializer<NavigatorNode>().Serialize(rootActivity);
                    Console.WriteLine(json);
                }
                    break;
            }
        }
    }
}