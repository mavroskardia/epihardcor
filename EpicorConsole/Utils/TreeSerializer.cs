using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace EpicorConsole.Utils
{
    public class TreeSerializer<T>
    {
        public string Serialize(Tree<T> tree)
        {
            var serializer = new DataContractJsonSerializer(typeof (Tree<T>));
            var stream = new MemoryStream();
            serializer.WriteObject(stream, tree);
            stream.Seek(0, SeekOrigin.Begin);
            return new StreamReader(stream).ReadToEnd();
        }

        public Tree<T> Deserialize(string treeStr)
        {
            var bytes = new byte[treeStr.Length*sizeof (char)];
            Buffer.BlockCopy(treeStr.ToCharArray(), 0, bytes, 0, bytes.Length);

            var stream = new MemoryStream(bytes);
            var serializer = new DataContractJsonSerializer(typeof (Tree<T>));

            return serializer.ReadObject(stream) as Tree<T>;
        }

        public string PrettyPrint(Tree<T> tree)
        {
            var builder = new StringBuilder();

            tree.Traverse(tree, (node, level) =>
            {
                for (int i = 0; i < level; i++)
                    builder.Append(' ');
                builder.AppendLine(node.Data.ToString());
            });

            return builder.ToString();
        }
    }
}