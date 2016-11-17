using NAudio.Wave;
using NAudio.Dsp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using NAudio.Wave.SampleProviders;
using MathNet.Numerics.LinearAlgebra;

namespace AudioSDGDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private WaveIn waveSource;
        public WaveFileWriter waveFile = null;

        private WaveOut player;

        private WaveFileReader wavReader;

        private bool recording = false;
        private bool playing = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnRecordClick(object sender, RoutedEventArgs e)
        {
            if (playing)
            {
                StopPlayback();
            }
            if (recording)
            {
                Record.Content = "Record";
                // stop recording
                waveSource.StopRecording();
                recording = false;
                return;
            }

            Record.Content = "Stop Record";

            waveSource = new WaveIn();
            waveSource.WaveFormat = new WaveFormat(44100, 16, 1);
                        
            waveSource.DataAvailable += new EventHandler<WaveInEventArgs>(waveSource_DataAvailable);
            waveSource.RecordingStopped += new EventHandler<StoppedEventArgs>(waveSource_RecordingStopped);

            waveFile = new WaveFileWriter("temp.wav", waveSource.WaveFormat);

            waveSource.StartRecording();

            recording = true;
        }

        void waveSource_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (waveFile != null)
            {
                waveFile.Write(e.Buffer, 0, e.BytesRecorded);
                waveFile.Flush();
            }
        }

        void waveSource_RecordingStopped(object sender, StoppedEventArgs e)
        {
            if (waveSource != null)
            {
                waveSource.Dispose();
                waveSource = null;
            }

            if (waveFile != null)
            {
                waveFile.Dispose();
                waveFile = null;
            }
        }

        private void PlayOriginal_Click(object sender, RoutedEventArgs e)
        {
            if(playing)
            {
                StopPlayback();
            }

            wavReader = new WaveFileReader("temp.wav");

            // set up playback
            player = new WaveOut();
            player.Init(wavReader);

            player.PlaybackStopped += Player_PlaybackStopped;


            // begin playback
            player.Play();
            playing = true;
        }

        private void PlayNew_Click(object sender, RoutedEventArgs e)
        {
            if (playing)
            {
                StopPlayback();
            }

            wavReader = new WaveFileReader("maybe-next-time.wav");
            StochasticResampleAudio(wavReader);


            wavReader = new WaveFileReader("temp2.wav");
            // set up playback
            player = new WaveOut();
            player.Init(wavReader);

            player.PlaybackStopped += Player_PlaybackStopped;

            // begin playback
            player.Play();
            playing = true;
        }

        private void Player_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            StopPlayback();
        }

        private void StopPlayback()
        {
            player.Stop();
            wavReader.Dispose();
            player.Dispose();
            playing = false;
        }

        private void StochasticResampleAudio(WaveFileReader wavReader)
        {
            ISampleProvider provider = new Pcm16BitToSampleProvider(wavReader);

            int blockSize = 2000;
            float[] buffer = new float[blockSize];
            List<float> FullBuffer = new List<float>();
            int rc;
            while ((rc = provider.Read(buffer, 0, blockSize)) > 0)
            {
                FullBuffer.AddRange(buffer.ToList());
            }
            var ToBeResampled = new List<Vector<float>>();
            for (int ii = 0; ii < FullBuffer.Count; ii++)
            {
                float[] temp = new float[2];
                temp[0] = ii;
                temp[1] = FullBuffer[ii];                
                ToBeResampled.Add(Vector<float>.Build.DenseOfArray(temp));
            }
            Gesture gest = new Gesture(ToBeResampled, "");
            var sr = gest.StochasticResample(gest.raw_pts, FullBuffer.Count, 0, 2.0f);

            //WaveFormat waveFormat = new WaveFormat(44100, 16, 1);

            WaveFileWriter outfile = new WaveFileWriter("temp2.wav", wavReader.WaveFormat);
  

            float[] newbuffer = new float[sr.Count];
            for (int ii = 0; ii < sr.Count; ii++)
            {
                if(ii >= 0 && sr[ii][0] % 1.0f >= float.Epsilon)
                {
                    //Console.WriteLine("Fix Me" + sr[ii][0]);
                    var inter_distance = ii - sr[ii][0];

                    newbuffer[ii] = sr[ii][1]*inter_distance + sr[ii][1]*(1.0f-inter_distance);
                }
                else
                {
                    newbuffer[ii] = sr[ii][1];
                }
            }

            outfile.WriteSamples(newbuffer, 0, newbuffer.Length);
            outfile.Close();
        }
    }
}
