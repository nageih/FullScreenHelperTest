using Lyricify_for_Spotify.Helpers.Device;

namespace FullScreenHelperTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            UserNotificationState? prevState = null;
            bool? prevFS = null;
            bool? prevGM = null;

            while (true)
            {
                var state = FullscreenHelper.State();
                var fs = FullscreenHelper.FullScreen;
                var gm = FullscreenHelper.GameMode;

                if (prevState != state || prevFS != fs || prevGM != gm)
                {
                    prevState = state;
                    prevFS = fs;
                    prevGM = gm;

                    Console.WriteLine($"{DateTime.Now:O} State={state} FullScreen={fs} GameMode={gm}");
                }

                // 降低 CPU 占用
                Thread.Sleep(100);
            }
        }
    }
}