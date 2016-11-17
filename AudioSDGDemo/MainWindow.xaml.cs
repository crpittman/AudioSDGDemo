using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using MathNet.Numerics.LinearAlgebra;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Numerics;

namespace AudioSDGDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
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

            wavReader = new WaveFileReader("temp.wav");
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

            Complex[] Samples = new Complex[FullBuffer.Count];
            
            for (int jj = 0; jj < Samples.Length; jj++)
            {
                Samples[jj] = new Complex(FullBuffer[jj], 0);                
            }
            Fourier.Forward(Samples, FourierOptions.Default);
            

            var ToBeResampled = new List<Vector<float>>();
            for (int ii = 0; ii < Samples.Length; ii++)
            {
                float[] temp = new float[2];
                //temp[0] = ii;
                //temp[1] = FullBuffer[ii];                
                temp[0] = (float)Samples[ii].Real;
                temp[1] = ii;
                ToBeResampled.Add(Vector<float>.Build.DenseOfArray(temp));
            }
            Gesture gest = new Gesture(ToBeResampled, "");
            var sr = gest.StochasticResample(gest.raw_pts, Samples.Length, 0, .025f);
            //TODO: Don't use index. Instead, interpolate and grab the appropriate points as they come, that way we don't lose the frequency bins.
            for (int ii = 0; ii < Samples.Length/2; ii++)
            {
                if (false)//(ii > 0) && (ii < Samples.Length-1) && (sr[ii][1] % 1.0f >= float.Epsilon))
                {
                    var current_freq = sr[ii][1];
                    var inter_distance = ii - current_freq;
                    if (inter_distance < 0)
                        Samples[ii] = new Complex(sr[ii-1][0] * inter_distance + sr[ii][0] * (1.0f - Math.Abs(inter_distance)), Samples[ii].Imaginary);
                    else
                        Samples[ii] = new Complex(sr[ii+1][0] * inter_distance + sr[ii][0] * (1.0f - Math.Abs(inter_distance)), Samples[ii].Imaginary);
                }
                else
                {                 
                    Samples[ii] = new Complex(sr[ii][0], Samples[ii].Imaginary);
                }                              
            }
            for (int ii = Samples.Length/2; ii < Samples.Length; ii++)
            {
                Samples[ii] = new Complex(sr[sr.Count-ii][0], Samples[ii].Imaginary);
            }

            Fourier.Inverse(Samples, FourierOptions.Default);

            WaveFileWriter outfile = new WaveFileWriter("temp2.wav", wavReader.WaveFormat);
            float[] newbuffer = new float[Samples.Length];
            for (int ii = 0; ii < Samples.Length; ii++)
            {     
                newbuffer[ii] = (float)Samples[ii].Real;      
            }
            outfile.WriteSamples(newbuffer, 0, newbuffer.Length);
            outfile.Close();
        }
    }
}
