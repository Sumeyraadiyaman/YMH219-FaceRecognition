using OpenCvSharp;

namespace YMH219_FaceRecognition
{
    // Yerel yüz örneklerini klasörlerde saklar: <kök>/<etiket>/<zaman>.png
    // Örnekler: Belgeler\YMH219-FaceRecognition\Yuzler\Sümeyra\20261125_140322_123.png
    public class FaceDataset
    {
        public string RootFolder { get; }

        public FaceDataset(string rootFolder)
        {
            RootFolder = rootFolder;
            Directory.CreateDirectory(rootFolder);
        }

        // Hazırlanmış (100x100 gri) bir yüz örneğini etiketin klasörüne kaydeder
        public void AddSample(string label, Mat face)
        {
            string folder = Path.Combine(RootFolder, MakeSafeFolderName(label));
            Directory.CreateDirectory(folder);

            string path = Path.Combine(folder, $"{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
            FaceImage.SavePng(path, face);
        }

        // Her etiketin kaç örneği olduğunu döndürür
        public Dictionary<string, int> GetSampleCounts()
        {
            return Directory.GetDirectories(RootFolder)
                .ToDictionary(
                    folder => Path.GetFileName(folder),
                    folder => Directory.GetFiles(folder, "*.png").Length);
        }

        // Bütün örnekleri (etiket + gri yüz görüntüsü) okur. Mat'lerin Dispose edilmesi çağıranın sorumluluğundadır.
        public List<(string Label, Mat Face)> LoadAllSamples()
        {
            List<(string Label, Mat Face)> samples = new List<(string Label, Mat Face)>();

            foreach (string folder in Directory.GetDirectories(RootFolder))
            {
                string label = Path.GetFileName(folder);

                foreach (string file in Directory.GetFiles(folder, "*.png"))
                {
                    try
                    {
                        Mat face = FaceImage.Load(file, ImreadModes.Grayscale);

                        // Boyutu farklı bir dosya eklenmişse eğitimden önce düzelt
                        if (face.Width != FaceImage.FaceSize || face.Height != FaceImage.FaceSize)
                            Cv2.Resize(face, face, new OpenCvSharp.Size(FaceImage.FaceSize, FaceImage.FaceSize));

                        samples.Add((label, face));
                    }
                    catch (Exception ex) when (ex is IOException || ex is InvalidDataException)
                    {
                        // Bozuk dosyayı atla, diğerleriyle devam et
                    }
                }
            }

            return samples;
        }

        // Klasör adında kullanılamayan karakterleri (\ / : * ? " < > |) temizler
        private static string MakeSafeFolderName(string label)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            string safe = new string(label.Trim().Where(c => !invalid.Contains(c)).ToArray());
            return safe.Length > 0 ? safe : "Etiketsiz";
        }
    }
}
