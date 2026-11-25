using OpenCvSharp;
using OpenCvSharp.Face;

namespace YMH219_FaceRecognition
{
    // Yüzün kime ait olduğunu tahmin eden sınıf (LBPH yöntemi).
    // LBPH, yüzü küçük karelere bölüp her karedeki doku desenini karşılaştırır.
    public class FaceRecognizerService : IDisposable
    {
        // LBPH "uzaklık" verir: küçük sayı = daha benzer. Bu değerin üstü "Bilinmiyor" sayılır.
        public const double UnknownThreshold = 80;

        private LBPHFaceRecognizer? recognizer;
        private readonly List<string> labelNames = new List<string>();

        // Kamera arka planda tahmin yaparken, arayüz aynı anda yeniden eğitim yapabilir; kilit bunu korur
        private readonly object sync = new object();

        public bool IsTrained
        {
            get { lock (sync) return recognizer != null; }
        }

        // Örneklerle modeli eğitir (en az iki farklı etiket gerekir)
        public void Train(List<(string Label, Mat Face)> samples)
        {
            List<string> names = samples.Select(s => s.Label).Distinct().OrderBy(n => n).ToList();

            if (names.Count < 2)
                throw new InvalidOperationException(
                    "Eğitim için en az iki farklı etiket gerekiyor.\n\n" +
                    "Örnek: Kendi yüzün için \"Sümeyra\", gözlüklü halin için \"Sümeyra-gözlük\" " +
                    "veya izin veren bir arkadaşın için ayrı bir etiket.");

            List<Mat> images = samples.Select(s => s.Face).ToList();
            List<int> labels = samples.Select(s => names.IndexOf(s.Label)).ToList(); // Etiketi sayıya çevir

            LBPHFaceRecognizer newRecognizer = LBPHFaceRecognizer.Create();
            newRecognizer.Train(images, labels);

            lock (sync)
            {
                recognizer?.Dispose();
                recognizer = newRecognizer;
                labelNames.Clear();
                labelNames.AddRange(names);
            }
        }

        // Hazırlanmış (100x100 gri) yüzün kime ait olduğunu ve uzaklık skorunu döndürür
        public (string Name, double Distance) Predict(Mat face)
        {
            lock (sync)
            {
                if (recognizer == null)
                    return ("Model eğitilmedi", 0);

                recognizer.Predict(face, out int label, out double distance);

                if (label < 0 || label >= labelNames.Count || distance > UnknownThreshold)
                    return ("Bilinmiyor", distance);

                return (labelNames[label], distance);
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                recognizer?.Dispose();
                recognizer = null;
            }
        }
    }
}
