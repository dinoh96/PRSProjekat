using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PRSProjekatJun2020
{
    class Buzen
    {
        private Vector<double> x;
        private Vector<double> G;
        public Vector<double> U;
        public Vector<double> X;
        public Vector<double> N;
        private Vector<double> R;
        private List<int> S;
        public double SystemResponse;

        public StreamWriter Output { get; }

        private int Kmin;
        private int Kmax;
        private int fixedServers;
        private int jobs;
        private int k;
        private int DeviceNumber;
        private int BottleNeck;

        private double g;

        public Buzen( List<int> S, StreamWriter outputFile, int fixedServers = 4, int Kmin = 2, int Kmax = 8)
        {
            this.Kmax = Kmax;
            this.Kmin = Kmin;
            this.fixedServers = fixedServers;
            this.S = S;
            Output = outputFile;
            DeviceNumber = fixedServers;
            k = -1;
            BottleNeck = -1;
        }
        
        public void compute(Vector<double> x, int k, int jobs, bool flush = true)
        {
            if (k < Kmin || k > Kmax) throw new Exception("Incorrect parameter: k\nValid values are from 2 to 8.\n");
            if (x == null || x.Count < DeviceNumber) throw new Exception("Incorrect parameter: x\nPlease use GordonNewell class to find values for x.\n");
            this.x = x;
            this.k = k;
            this.jobs = jobs;
            DeviceNumber = fixedServers + k;

            computeG();
            g = G[jobs - 1] / G[jobs];
            computeU();
            computeX();
            computeN();
            computeR();

            SystemResponse = (double)jobs / X[0];

            if (flush) Flush();
        }
        public Vector<double> computeG()   // double check this
        {
            if (k < Kmin || k > Kmax) throw new Exception("Incorrect parameter: k\nValid values are from 2 to 8.\n");
            G = Vector<double>.Build.Dense(jobs + 1);
            G[0] = 1;
            for (int i = 1; i <= jobs; G[i++] = 0) ;

            for (int j = 0; j < DeviceNumber; j++)
                for (int i = 1; i <= jobs; i++)
                    G[i] += x[j] * G[i - 1];
            return G;
        }

        public Vector<double> computeU()
        {
            U = Vector<double>.Build.Dense(DeviceNumber);
            double max = -1;
            for (int i = 0; i < DeviceNumber; i++)
            {
                U[i] = x[i] * g;
                if (U[i] > max)
                {
                    max = U[i];
                    BottleNeck = i;
                }
            }
            return U;
        }
        public Vector<double> computeX()
        {
            X = Vector<double>.Build.Dense(DeviceNumber);
            for (int i = 0; i < DeviceNumber; i++)
                X[i] = U[i] / S[i] * 1000;  // * 1000 jer je u milisekundama 
            return X;
        }
        public Vector<double> computeN()
        {
            N = Vector<double>.Build.Dense(DeviceNumber);
            for(int i = 0; i < DeviceNumber; i++)
            {
                double temp = x[i];    // ako u formuli treba dodati element x[i]^n*G[0] i ako je G[0] = 1 => pocetna vrednost za temp treba da bude x[i]
                for (int j = jobs - 1; j > 0; j--)
                {
                    temp *= x[i];
                    temp += x[i] * G[jobs - j];                
                }
                N[i] = temp/G[jobs];
            }
            return N;
        }
        public Vector<double> computeR()
        {
            R = Vector<double>.Build.Dense(DeviceNumber);
            for (int i = 0; i < DeviceNumber; i++)
                R[i] = N[i] / X[i];
            return R;
        }

        public void Flush()
        {
            Output.WriteLine("K: {0}\n\t\tJobs: {1}", k, jobs);
            Output.Write("\t\t\t\t\t\tCPU\t\tSD1\t\tSD2\t\tSD3\t\tUD1\t\tUD2");
            for (int i = 3; i <= k; i++) Output.Write("\t\tSD{0}", i);
            Output.Write("\n\t\t\t\t\t\t");
            for (int i = 0; i < DeviceNumber; i++) Output.Write("========");
            Output.WriteLine();

            Output.Write("Iskoriscenje resursa\t");
            foreach (double tmp in U)
                Output.Write("{0:0.000}\t", tmp);
            Output.WriteLine();

            Output.Write("Protoci kroz resurse\t");
            foreach (double tmp in X)
                Output.Write("{0:0.000}\t", tmp);
            Output.WriteLine();

            Output.Write("Prosecan broj poslova\t");
            foreach (double tmp in N)
                Output.Write("{0:0.000}\t", tmp);
            Output.WriteLine();

            Output.Write("Vreme odziva sistema\t");
            Output.WriteLine("{0:0.000}ms", SystemResponse);                       // PRS10_b, slajd 22  EDIT 6\7: sklonio * 100

            Output.Write("Kritican resurs sistema\t{0} - ", BottleNeck);
            
            string BottleNeckName;
            switch (BottleNeck)
            {
                case 0: BottleNeckName = "CPU";
                    break;
                case 1:
                case 2:
                case 3: BottleNeckName = "SD" + BottleNeck;
                    break;
                case -1: BottleNeckName = "Error, critical resource is not set.";
                        break;
                default: BottleNeckName = "UD" + (BottleNeck - 3).ToString();
                    break;
            }
            Output.WriteLine("{0}\n", BottleNeckName);

            for (int i = 0; i < DeviceNumber + 3; i++) Output.Write("********");
            Output.WriteLine("\n");

        }
        public void Clean()
        {
            Output.Close();
        }
    }
}
