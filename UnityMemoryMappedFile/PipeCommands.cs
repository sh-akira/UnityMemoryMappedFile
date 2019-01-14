using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace UnityMemoryMappedFile
{
    public class PipeCommands
    {
        public static Type GetCommandType(string commandStr)
        {
            var commands = typeof(PipeCommands).GetNestedTypes(System.Reflection.BindingFlags.Public);
            foreach (var command in commands)
            {
                if (command.Name == commandStr) return command;
            }
            return null;
        }

        public class SendMessage
        {
            public string Message { get; set; }
        }

        public class MoveObject
        {
            public float X { get; set; }
        }

        public class GetCurrentPosition
        {
        }

        public class ReturnCurrentPosition
        {
            public float CurrentX { get; set; }
        }
    }
}
