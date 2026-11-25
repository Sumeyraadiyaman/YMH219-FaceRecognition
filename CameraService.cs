using OpenCvSharp;

namespace YMH219_FaceRecognition
{
    // Kameradan sürekli görüntü okuyan sınıf. Okuma arka planda yapılır, arayüz donmaz.
    public class CameraService : IDisposable
    {
        private VideoCapture? capture;
        private CancellationTokenSource? cancellation;
        private Task? captureTask;

        public bool IsRunning => captureTask != null && !captureTask.IsCompleted;

        // Her yeni karede tetiklenir (arka plan iş parçacığından!).
        // Gelen Mat bir sonraki karede yeniden kullanılır; saklanacaksa Clone() alınmalıdır.
        public event Action<Mat>? FrameCaptured;

        // Kamera beklenmedik şekilde koparsa tetiklenir
        public event Action<string>? CameraError;

        // Kamerayı açar. Kamera yoksa veya başka uygulama kullanıyorsa hata fırlatır.
        public void Start(int cameraIndex = 0)
        {
            if (IsRunning)
                return;

            VideoCapture newCapture = new VideoCapture(cameraIndex, VideoCaptureAPIs.DSHOW);
            if (!newCapture.IsOpened())
            {
                newCapture.Dispose();
                throw new InvalidOperationException(
                    "Kamera açılamadı. Kameranın bağlı olduğundan, başka bir uygulamanın (Zoom, Teams vb.) " +
                    "kullanmadığından ve Windows gizlilik ayarlarında kamera izninin açık olduğundan emin olun.");
            }

            capture = newCapture;
            cancellation = new CancellationTokenSource();
            CancellationToken token = cancellation.Token;
            captureTask = Task.Run(() => CaptureLoop(newCapture, token));
        }

        // Kamerayı kapatır
        public void Stop()
        {
            cancellation?.Cancel();

            try
            {
                captureTask?.Wait(2000); // Okuma döngüsünün bitmesini en fazla 2 saniye bekle
            }
            catch (AggregateException)
            {
                // Döngü hatayla bittiyse önemli değil
            }

            capture?.Release();
            capture?.Dispose();
            capture = null;
            captureTask = null;
        }

        // Arka planda çalışan okuma döngüsü
        private void CaptureLoop(VideoCapture source, CancellationToken token)
        {
            using Mat frame = new Mat();
            int failedReads = 0;

            while (!token.IsCancellationRequested)
            {
                if (!source.Read(frame) || frame.Empty())
                {
                    // Arka arkaya ~1 saniye görüntü gelmezse kamera kopmuş demektir
                    if (++failedReads > 30)
                    {
                        CameraError?.Invoke("Kameradan görüntü alınamıyor. Kamera bağlantısını kontrol edin.");
                        return;
                    }

                    Thread.Sleep(30);
                    continue;
                }

                failedReads = 0;
                FrameCaptured?.Invoke(frame);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
