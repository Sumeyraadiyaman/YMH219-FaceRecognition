using OpenCvSharp;

namespace YMH219_FaceRecognition
{
    // Bir karede bulunan tek bir yüz: yeri, tahmin edilen etiket ve uzaklık skoru
    public record DetectedFace(Rect Area, string Name, double Distance);

    // Algılama ve tanımayı birleştiren sınıf: bir kare alır, içindeki yüzleri bulur ve tanır.
    public class FaceAnalyzer : IDisposable
    {
        private readonly FaceDetector detector;
        private readonly FaceRecognizerService recognizer;

        // "Örnek Ekle" için son karedeki en büyük yüz saklanır
        private Mat? lastLargestFace;
        private readonly object sync = new object();

        public FaceAnalyzer(FaceDetector detector, FaceRecognizerService recognizer)
        {
            this.detector = detector;
            this.recognizer = recognizer;
        }

        // Renkli (BGR) karedeki bütün yüzleri bulur ve tanır
        public List<DetectedFace> Analyze(Mat colorFrame)
        {
            using Mat gray = new Mat();
            Cv2.CvtColor(colorFrame, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.EqualizeHist(gray, gray);

            // Büyükten küçüğe sırala: ilk yüz kameraya en yakın kişi
            Rect[] faces = detector.Detect(gray)
                .OrderByDescending(r => r.Width * r.Height)
                .ToArray();

            List<DetectedFace> results = new List<DetectedFace>();
            Mat? largest = null;

            foreach (Rect area in faces)
            {
                using Mat face = FaceImage.PrepareFace(gray, area);

                if (largest == null)
                    largest = face.Clone();

                (string name, double distance) = recognizer.Predict(face);
                results.Add(new DetectedFace(area, name, distance));
            }

            lock (sync)
            {
                lastLargestFace?.Dispose();
                lastLargestFace = largest;
            }

            return results;
        }

        // Son karedeki en büyük yüzün kopyasını verir (yüz yoksa null)
        public Mat? GetLastFace()
        {
            lock (sync)
            {
                return lastLargestFace?.Clone();
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                lastLargestFace?.Dispose();
                lastLargestFace = null;
            }
        }
    }
}
