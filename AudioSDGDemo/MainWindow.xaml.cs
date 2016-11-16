﻿using NAudio.Wave;
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
            waveSource.WaveFormat = new WaveFormat(44100, 1);

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
            //Do SDG with the data here

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
    }
}
