using System.Collections.Generic;
using System.IO;

namespace ADPClient
{
    /// <summary>
    /// 
    /// </summary>
    public static class Utility
    {
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="filePath"></param>
        /// <param name="header"></param>
        /// <returns></returns>
        public static void ExportCSV<T>(List<T> list, string filePath, string header)
        {
            using (StreamWriter sw = new StreamWriter(filePath))
            {
                sw.WriteLine(header);
                foreach (var line in list)
                {
                    sw.WriteLine(line);
                }
            }
        }
    }
}
