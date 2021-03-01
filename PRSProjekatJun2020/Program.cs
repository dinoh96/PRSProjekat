using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace PRSProjekatJun2020
{
    class Program
    {
        private static Matrix<double> P { get; set; }

        private static List<int> S { get; set; }

        private static int fixedServers;
        private static int Kmin;
        private static int Kmax;

        private static List<int> KValues;
        private static List<int> MPDegree;
        private static List<int> SimulationParameters;


        public static void FillP(int k)
        {
            P = Matrix<double>.Build.Dense(fixedServers + k, fixedServers + k);
            int i, j;
            for (i = 0; i < fixedServers; i++) P[0, i] = 0.1;
            for (i = 1; i < fixedServers + k; i++)
                for (j = 0; j < fixedServers; j++)
                    P[i, j] = (j == 0 ? (i < fixedServers ? 0.4 : 1) : 0);
            double UserDiskProbability = 0.6 / k;
            for (i = 0; i < fixedServers + k; i++)
                for (j = fixedServers; j < fixedServers + k; j++)
                    P[i, j] = (i < fixedServers ? UserDiskProbability : 0);
        }

        public static void init()
        {
            fixedServers = 4;
            Kmin = 8;
            Kmax = 8;
            FillS();
            FillKValues();
            FillMultiProgrammingDegree();
            FillSimulationParameters();
        }

        private static void FillSimulationParameters()
        {
            SimulationParameters = new List<int>(3)
            {
                //10,
                //25,
                1
            };
        }

        public static void FillMultiProgrammingDegree()
        {
            MPDegree = new List<int>(3)
            {
                //10,
                //15,
                20
            };
        }

        public static void FillKValues()
        {
            KValues = new List<int>(Kmax - Kmin + 1);
            for (int i = Kmin; i <= Kmax; i++)
                KValues.Add(i);
        }

        public static void FillS()
        {
            S = new List<int>(fixedServers + Kmax)
            {
                5,  // CPU
                20, // System Disk 1
                15, // System Disk 2
                15  // System Disk 3
            };
            for (int i = 4; i < 12; i++) S.Add(20);  // User Disks
        }

        static void Main(string[] args)
        {
            Console.WriteLine(DateTime.Now);


            if (args.Length < 1) throw new Exception("Enter path to this file as first argument! ");

            string path = args[0];

            int SimulationMinutes = (args.Length >= 2) ? Int32.Parse(args[1]) : (24 * 60);
            int factor = 24 * 60 / SimulationMinutes;


            /* FilePaths[0] = potraznje analiticki
             * FilePaths[1] = rezultati analiticki
             * FilePaths[2] = rezultati simulacija
             * FilePaths[3] = rezultati simulacija usrednjeno
             * FilePaths[4] = odstupanje simulacija 
             * FilePaths[5] = odstupanje simulacija usrenjeno
             */
            string[] FilePaths = { @"\potraznje_analiticki.txt", @"\rezultati_analiticki.txt", @"\rezultati_simulacija.txt", @"\rezultati_simulacija_usrednjeno.txt", @"\odstupanje_simulacija.txt", @"\odstupanje_simulacija_usrednjeno.txt" };

            for (int i = 0; i < FilePaths.Length; i++)
                FilePaths[i] = path + FilePaths[i];

            foreach (string FilePath in FilePaths)
            {
                using (StreamWriter File = new StreamWriter(FilePath))
                {
                    // kreiranje praznih datoteka
                }
            }

            Stopwatch sw = new Stopwatch();


            /*  args[2] == 0 -> Sequential 
             *  args[2] == 1 -> Parallel K
             *  args[2] == 2 -> Parallel all
             *  stop == true -> run only one scenario
             */
            

           
            sw.Restart();
            sw.Start();

            init();                

            GordonNewell GN = new GordonNewell(S, FilePaths[0]);
            Buzen B = null;// new Buzen(S, FilePaths[1]);
            
            

            bool simulationTest = true;

            if (simulationTest)
            {

                int maxNumOfSimulations = 0;
                int oldCnt = 0;

                Simulation[] simulations = new Simulation[MPDegree.Count * KValues.Count];
                Thread[] threads = new Thread[MPDegree.Count * KValues.Count];
                Buzen[] buzens = new Buzen[MPDegree.Count * KValues.Count];
                StreamWriter buzenFile = new StreamWriter(FilePaths[1]);
                StreamWriter simOutputFile = new StreamWriter(FilePaths[2]);
                StreamWriter simMeanOutputFile = new StreamWriter(FilePaths[3]);
                StreamWriter simDiviationOutputFile = new StreamWriter(FilePaths[4]);
                StreamWriter simMeanDiviationOutputFile = new StreamWriter(FilePaths[5]);

                int SleepTime = 100;

                for (int jobIndex = 0; jobIndex < MPDegree.Count; jobIndex++)
                {
                    //simulations[jobIndex] = new Simulation(S, MPDegree[jobIndex], SimulationParameters[jobIndex], simOutputFile, simMeanOutputFile, simDiviationOutputFile, simMeanDiviationOutputFile, SimulationMinutes);
                    //buzens[jobIndex] = new Buzen(S, buzenFile);

                    maxNumOfSimulations += SimulationParameters[jobIndex];
                }

                maxNumOfSimulations *= Kmax - Kmin + 1;

                for (int i = 0; i < 100; i++)
                {
                    if (i == 0)
                    {
                        Console.Write(0);
                        continue;
                    }
                    if (i == 97)
                    {
                        Console.WriteLine(100);
                        break;
                    }
                    Console.Write(' ');
                }
                int passed = 0;

                int simIndex = -1;

                foreach (int k in KValues)
                {
                    Vector<double> x = GN.getX(k);

                    for (int jobIndex = 0; jobIndex < MPDegree.Count; jobIndex++)
                    {
                        ++simIndex;
                        simulations[simIndex] = new Simulation(S, MPDegree[jobIndex], SimulationParameters[jobIndex], simOutputFile, simMeanOutputFile, simDiviationOutputFile, simMeanDiviationOutputFile, SimulationMinutes);
                        buzens[simIndex] = new Buzen(S, buzenFile);
                        buzens[simIndex].compute(x, k, MPDegree[jobIndex]);

                        simulations[simIndex].SetB(buzens[simIndex]);
                        simulations[simIndex].Init(GN.Probabilities, k);

                        ThreadStart TS = new ThreadStart(simulations[simIndex].Start);
                        threads[simIndex] = new Thread(TS);
                        threads[simIndex].Start();

                    }
                }
                StreamWriter queue_exec = new StreamWriter(path + @"\queue_exec.txt");
                for (int threadIndex = 0; threadIndex <= simIndex;)
                {
                    if (threads[threadIndex].IsAlive)
                    {
                        int newPassed = 100 * Simulation.cnt / maxNumOfSimulations;
                        //Console.WriteLine("{0}-{1}", newPassed, passed);

                        if (newPassed > passed)
                        {
                            for (int i = 0; i < newPassed - passed; i++)
                                Console.Write('*');
                            passed = newPassed;
                        }
                        // ako nit nije zavrsila, odspavaj
                        Thread.Sleep(SleepTime);

                    }
                    else
                    {
                        simulations[threadIndex].Flush(true);
                        simulations[threadIndex].Flush(false);
                        simulations[threadIndex].Flush(true, true);
                        simulations[threadIndex].Flush(false, true);
                        simulations[threadIndex].print(queue_exec);
                        threadIndex++;
                    }
                }


                GN.Flush();

                GN.Clean();
                buzenFile.Close();
                simOutputFile.Close();
                simMeanOutputFile.Close();
                simDiviationOutputFile.Close();
                simMeanDiviationOutputFile.Close();
                queue_exec.Close();


                sw.Stop();

                long elapsed = sw.ElapsedMilliseconds;
                float seconds = elapsed / 1000;
                float minutes = seconds / 60;
                float hours = minutes / 60;
                Console.WriteLine("\npassed                      {0} hrs = {1} minutes = {2} seconds", Math.Round(hours, 2), Math.Round(minutes, 2), Math.Round(seconds, 2));
                seconds *= factor;
                minutes = seconds / 60;
                hours = minutes / 60;
                Console.WriteLine("full simulation expectanncy {0} hrs = {1} minutes = {2} seconds\n", Math.Round(hours, 2), Math.Round(minutes, 2), Math.Round(seconds, 2));

                //for(int i = 0; i < 12; i++)
                //{
                //    Console.Write("{0:0.000}\t", 1.0 * Simulation.processTimes[i] / Simulation.processJobs[i]);
                //}
                //Console.WriteLine();

                for (int i = 0; i < Kmax + 4; i++)
                {
                    for (int j = 0; j < Kmax + 4; j++)
                        Console.Write("{0:0.000}\t", Simulation.mat[i, j] / Simulation.numberOfJobs[i]);
                    Console.WriteLine();
                }

            }
            else
            {

                // jedna nit jedna kombinacija parametara
                int SleepTime = 60;
                int Simulations = (Kmax - Kmin + 1) * MPDegree.Count;
                int SimNumber = 0;

                ThreadedSimulation[] tSims = new ThreadedSimulation[Simulations];
                for (int i = 0; i < Simulations; i++)
                    tSims[i] = new ThreadedSimulation(S, null, FilePaths[2], FilePaths[3], FilePaths[4], FilePaths[5], SimulationMinutes);

                Thread[] Threads = new Thread[Simulations];

                int repetitions = -1;

                foreach (int Jobs in MPDegree)
                {
                    repetitions++;
                    foreach (int k in KValues)
                    {
                        Vector<double> x = GN.getX(k);
                        B.compute(x, k, Jobs);

                        tSims[SimNumber].SimulationParameters = SimulationParameters.GetRange(repetitions, 1);
                        tSims[SimNumber].SetB(B);

                        tSims[SimNumber].Init(GN.Probabilities, k, Jobs);
                        ThreadStart TS = new ThreadStart(tSims[SimNumber].Run);
                        Threads[SimNumber] = new Thread(TS);
                        Threads[SimNumber].Start();
                        SimNumber++;
                    }
                }
                SimNumber = 0;
                // redom cekanje da zavrse niti i upisivanje u fajlove
                while (SimNumber < Simulations)
                {
                    if (Threads[SimNumber].IsAlive)
                    {
                        // ako nit nije zavrsila, odspavaj
                        Thread.Sleep(SleepTime);
                    }
                    else
                    {
                        tSims[SimNumber].Print2();
                        tSims[SimNumber].Print();
                        tSims[SimNumber].Compare();
                        SimNumber++;
                    }
                }
                SimNumber = 0;
                // zavrsile sve niti za trenutni stepen multiprogramiranja


                GN.Flush();

                GN.Clean();
                B.Clean();

                sw.Stop();

                long elapsed = sw.ElapsedMilliseconds;
                float seconds = elapsed / 1000;
                float minutes = seconds / 60;
                float hours = minutes / 60;
                Console.WriteLine("passed                      {0} hrs = {1} minutes = {2} seconds", Math.Round(hours, 2), Math.Round(minutes, 2), Math.Round(seconds, 2));
                seconds *= factor;
                minutes = seconds / 60;
                hours = minutes / 60;
                Console.WriteLine("full simulation expectanncy {0} hrs = {1} minutes = {2} seconds", Math.Round(hours, 2), Math.Round(minutes, 2), Math.Round(seconds, 2));

            }


        }
    }
}
