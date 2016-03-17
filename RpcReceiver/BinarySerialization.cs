using System;
using System.IO;
using NLog;

namespace RpcReceiver
{
    public static class BinarySerialization
    {
        private static Logger _logger = LogManager.GetLogger("General");

        public static void WriteToBinaryFile<T>(string filePath, T objectToWrite, bool append = false)
        {
            using (Stream stream = File.Open(filePath, append ? FileMode.Append : FileMode.Create))
            {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                binaryFormatter.Serialize(stream, objectToWrite);
            }
        }

        public static T ReadFromBinaryFile<T>(string filePath) where T:class 
        {
            try
            {
                using (Stream stream = File.Open(filePath, FileMode.Open))
                {
                    var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    return (T) binaryFormatter.Deserialize(stream);
                }
            }
            catch(Exception e)
            {
                _logger.Log(LogLevel.Fatal,"Unable to read binary file {0} with message {1} exception :{2},  ",filePath, e.Message,e.StackTrace);
                return null;
            }
            
        }
    }
}