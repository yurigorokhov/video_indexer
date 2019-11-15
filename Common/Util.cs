using System.IO;

namespace My.VideoIndexer.Common {

    public static class Util {

        //--- Static Methods ---
        public static string GetAudioS3Key(string videoEtag) {
            return Path.Combine("audio", $"{videoEtag}.mp3");
        }
    }
}