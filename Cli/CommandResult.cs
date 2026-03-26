using System;
using System.Text;
using System.Web.Script.Serialization;
using System.Collections.Generic;
using System.Linq;

namespace GARbro.Cli
{
    public static class JsonFormatter
    {
        public static string Serialize(object obj, bool pretty)
        {
            var serializer = new JavaScriptSerializer();
            // max depth config
            serializer.MaxJsonLength = Int32.MaxValue;
            var json = serializer.Serialize(obj);

            if (pretty)
            {
                return FormatOutput(json);
            }
            return json;
        }

        private static string FormatOutput(string json)
        {
            var stringBuilder = new StringBuilder();
            bool escaping = false;
            bool inQuotes = false;
            int indentation = 0;

            foreach (char character in json)
            {
                if (escaping)
                {
                    escaping = false;
                    stringBuilder.Append(character);
                }
                else
                {
                    if (character == '\\')
                    {
                        escaping = true;
                        stringBuilder.Append(character);
                    }
                    else if (character == '\"')
                    {
                        inQuotes = !inQuotes;
                        stringBuilder.Append(character);
                    }
                    else if (!inQuotes)
                    {
                        if (character == ',' )
                        {
                            stringBuilder.Append(character);
                            stringBuilder.Append("\r\n");
                            stringBuilder.Append(new string(' ', indentation * 2));
                        }
                        else if (character == '[' || character == '{')
                        {
                            stringBuilder.Append(character);
                            stringBuilder.Append("\r\n");
                            stringBuilder.Append(new string(' ', ++indentation * 2));
                        }
                        else if (character == ']' || character == '}')
                        {
                            stringBuilder.Append("\r\n");
                            stringBuilder.Append(new string(' ', --indentation * 2));
                            stringBuilder.Append(character);
                        }
                        else if (character == ':')
                        {
                            stringBuilder.Append(character);
                            stringBuilder.Append(' ');
                        }
                        else
                        {
                            stringBuilder.Append(character);
                        }
                    }
                    else
                    {
                        stringBuilder.Append(character);
                    }
                }
            }

            return stringBuilder.ToString();
        }
    }
}
