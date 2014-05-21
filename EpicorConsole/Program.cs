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
            if (args.Length == 1)
            {
                string serializedData = new StreamReader(File.OpenRead(args[0])).ReadToEnd();
                var serializer = new TreeSerializer<NavigatorNode>();
                Tree<NavigatorNode> tree = serializer.Deserialize(serializedData);
                Console.WriteLine(serializer.PrettyPrint(tree));
            }
            else if (args.Length == 2)
            {
                List<Time> charges = new Epicor().GetCurrentCharges();
                foreach (Time charge in charges)
                {
                    Console.WriteLine(charge);
                }
            }
            else
            {
                var epicor = new Epicor();
                Tree<NavigatorNode> rootActivity = epicor.GetSiteActivities();
                string json = new TreeSerializer<NavigatorNode>().Serialize(rootActivity);
                Console.WriteLine(json);
            }
        }
    }
}