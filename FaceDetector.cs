using OpenCvSharp;

namespace YMH219_FaceRecognition
{
    // Görüntüde yüzlerin nerede olduğunu bulan sınıf (Haar Cascade yöntemi)
    public class FaceDetector : IDisposable
    {
        private readonly CascadeClassifier cascade;

        public FaceDetector(string cascadePath)
        {
            if (!File.Exists(cascadePath))
                throw new FileNotFoundException("Yüz algılama modeli bulunamadı.", cascadePath);

            cascade = new CascadeClassifier(cascadePath);

            if (cascade.Empty())
            {
                cascade.Dispose();
                throw new InvalidDataException("Yüz algılama modeli yüklenemedi: " + cascadePath);
            }
        }

        // Gri görüntüdeki yüzlerin dikdörtgenlerini döndürür
        public Rect[] Detect(Mat gray)
        {
            return cascade.DetectMultiScale(
                gray,
                scaleFactor: 1.1,                         // Her adımda görüntüyü %10 küçülterek ara
                minNeighbors: 5,                          // Yanlış algılamaları azaltır (yüksek = daha seçici)
                flags: HaarDetectionTypes.ScaleImage,
                minSize: new OpenCvSharp.Size(60, 60));   // 60 pikselden küçük yüzleri yok say
        }

        public void Dispose()
        {
            cascade.Dispose();
        }
    }
}
