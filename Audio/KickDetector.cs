using Audio;
using FftSharp;
using NAudio.Dsp;
using System.Drawing;

public class KickDetector
{
    private const int KickMinFrequency = 30;
    private const int KickMaxFrequency = 200;
    private double kickThreshhold, timeThreshhold;
    private int sampleRate;
    private int fftSize;
    private int minKickBin;
    private int maxKickBin;

    public event Action KickDetected;

    private BiQuadFilter bandPassFilter;

    public KickDetector(int sampleRate, int fftSize, double kickThreshhold, double timeThreshhold)
    {
        this.sampleRate = sampleRate;
        this.fftSize = fftSize;
        this.kickThreshhold = kickThreshhold;
        this.timeThreshhold = timeThreshhold;
        this.minKickBin = FrequencyToBin(KickMinFrequency, sampleRate, fftSize);
        this.maxKickBin = FrequencyToBin(KickMaxFrequency, sampleRate, fftSize);
        this.bandPassFilter = BiQuadFilter.BandPassFilterConstantSkirtGain(sampleRate, (KickMinFrequency + KickMaxFrequency) / 2.0f, 1.0f);
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
        double kickMagnitude = GetKickMagnitude(fftResult);

        // Check for amplitude spike in time domain
        double timeDomainPeak = GetTimeDomainPeak(filteredFrame);

        //Console.WriteLine(kickMagnitude);
        //Console.WriteLine(timeDomainPeak);

        // Combine frequency and time domain features
        if (kickMagnitude > kickThreshhold && timeDomainPeak > timeThreshhold)
        {
            OnKickDetected();
        }
        else
        {
            kicked = false;
        }
    }

    private double GetKickMagnitude(System.Numerics.Complex[] fftResult)
    {
        double magnitudeSum = 0;
        for (int i = minKickBin; i <= maxKickBin; i++)
        {
            magnitudeSum += fftResult[i].Magnitude;
        }
        return magnitudeSum / (maxKickBin - minKickBin + 1);
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
    bool kicked = false;
    protected virtual void OnKickDetected()
    {
        if(!kicked)
        {
            kicked = true;
            onKickEnter();
        }
        KickDetected?.Invoke();
    }
    int colIndex = 0;
    Color[] colArray = { Color.Red, Color.Blue, Color.Green, Color.Yellow, Color.White };
    void onKickEnter()
    {
        Effect_Flash flash = new Effect_Flash(Audio.Audio.renderX, Audio.Audio.renderY, 100, colArray[colIndex]);
        flash.Start();
        colIndex++;
        if (colIndex >= colArray.Length) colIndex = 0;
        //Audio.Audio.GenerateNextColorFrame();
        Console.WriteLine("kick");
    }
}