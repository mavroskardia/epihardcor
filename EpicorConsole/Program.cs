using System;
using System.IO;
using EpicorConsole.Utils;
using EpicorLibrary;
using EpicorLibrary.Utils;

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
                    var epicor = new Epicor();
                    var charges = epicor.GetCurrentCharges(DateTime.Today, epicor.GetResourceId());
                    foreach (var charge in charges)
                    {
                        Console.WriteLine(charge);
                    }
                }
                    break;
                default:
                {
                    var epicor = new Epicor();
                    var rootActivity = epicor.GetSiteActivities(epicor.GetResourceId());
                    var json = new TreeSerializer<NavigatorNode>().Serialize(rootActivity);
                    Console.WriteLine(json);
                }
                    break;
            }
        }
    }
}