﻿using System.IO;

namespace Varekai.Utils
{
    public static class JsonFileUtils
    {
        public static string ReadJsonFromFile(string path)
        {
            using (var stream = new StreamReader(path))
            {
                return stream.ReadToEnd();
            }
        }
    }
}