using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using EpicorLibrary.Utils;

namespace EpicorConsole.Utils
{
    [DataContract]
    [KnownType(typeof (NavigatorNode))]
    public class Tree<T>
    {
        [DataMember] public LinkedList<Tree<T>> Children;

        public Tree()
        {
            Children = new LinkedList<Tree<T>>();
        }

        public Tree(T data)
        {
            Data = data;
            Children = new LinkedList<Tree<T>>();
        }

        [DataMember]
        public T Data { get; set; }

        public void AddChild(T childData)
        {
            Children.AddLast(new Tree<T>(childData));
        }

        public Tree<T> GetChild(int i)
        {
            return Children.FirstOrDefault(n => --i == 0);
        }

        public void Traverse(Tree<T> node, Action<Tree<T>, int> visitor, int level = 0)
        {
            visitor(node, level);
            foreach (var kid in node.Children)
                Traverse(kid, visitor, level + 1);
        }
    }
}