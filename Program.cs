namespace YMH219_FaceRecognition
{
    internal static class Program
    {
        // Uygulamanın başlangıç noktası
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
