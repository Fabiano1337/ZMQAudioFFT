using Audio;
using FftSharp;
using NAudio.Dsp;
using System;
using System.Drawing;

public class FrequencyDetector
{
    private int minFrequency;
    private int maxFrequency;
    private double threshhold, timeThreshhold;
    private int sampleRate;
    private int fftSize;
    private int minBin;
    private int maxBin;

    public event Action<double> onFrequency;

    private BiQuadFilter bandPassFilter;

    public FrequencyDetector(int sampleRate, int fftSize, double threshhold, double timeThreshhold, int minFrequency, int maxFrequency)
    {
        this.minFrequency = minFrequency;
        this.maxFrequency = maxFrequency;
        this.sampleRate = sampleRate;
        this.fftSize = fftSize;
        this.threshhold = threshhold;
        this.timeThreshhold = timeThreshhold;
        this.minBin = FrequencyToBin(minFrequency, sampleRate, fftSize);
        this.maxBin = FrequencyToBin(maxFrequency, sampleRate, fftSize);
        this.bandPassFilter = BiQuadFilter.BandPassFilterConstantSkirtGain(sampleRate, (minFrequency + maxFrequency) / 2.0f, 1.0f);
    }

    public void ProcessAudioFrame(float[] audioFrame)
    {
        if (audioFrame.Length != fftSize)
        {
            throw new ArgumentException($"Audio frame size must be equal to fftSize ({fftSize}).");
        }

        // Apply bandpass filter
        float[] filteredFrame = new float[audioFrame.Length];
        for (int i = 0; i < audioFrame.Length; i++)
        {
            filteredFrame[i] = bandPassFilter.Transform(audioFrame[i]);
        }

        // Perform FFT
        System.Numerics.Complex[] fftResult = new System.Numerics.Complex[fftSize];
        for (int i = 0; i < fftSize; i++)
        {
            fftResult[i] = new System.Numerics.Complex(filteredFrame[i], 0.0);
        }
        FFT.Forward(fftResult);

        // Calculate magnitudes in the kick drum frequency range
        double magnitude = GetMagnitude(fftResult);

        // Check for amplitude spike in time domain
        double timeDomainPeak = GetTimeDomainPeak(filteredFrame);

        //Console.WriteLine(magnitude);
        //Console.WriteLine(timeDomainPeak);

        // Combine frequency and time domain features
        if (magnitude > threshhold) //  && timeDomainPeak > timeThreshhold
        {
            OnFrequencyDetected(magnitude);
        }
        else
        {
            if(magnitude < threshhold*0.85f) triggered = false;
        }
    }

    private double GetMagnitude(System.Numerics.Complex[] fftResult)
    {
        double magnitudeSum = 0;
        for (int i = minBin; i <= maxBin; i++)
        {
            magnitudeSum += fftResult[i].Magnitude;
        }
        return magnitudeSum / (maxBin - minBin + 1);
    }

    private double GetTimeDomainPeak(float[] audioFrame)
    {
        double maxAmplitude = 0.0;
        foreach (var sample in audioFrame)
        {
            maxAmplitude = Math.Max(maxAmplitude, Math.Abs(sample));
        }
        return maxAmplitude;
    }

    private int FrequencyToBin(int frequency, int sampleRate, int fftSize)
    {
        return (int)(frequency * fftSize / sampleRate);
    }
    bool triggered = false;
    protected virtual void OnFrequencyDetected(double amplitude)
    {
        if (!triggered)
        {
            triggered = true;
            //onFrequencyEnter();
            onFrequency?.Invoke(amplitude);
        }
    }

    void onFrequencyEnter()
    {
        //Audio.Audio.GenerateNextColorFrame();
        //Console.WriteLine("frequency");
    }
}