using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using ExcelDataReader;

// Top-level code starts here
string filePath = "C:\\Users\\R-A-P-S\\Downloads\\Optimisation\\ClusteringProject\\OnlineRetail.xlsx";

int numParticles = 30;
int numClusters = 3;
int numIterations = 100;

var customers = PSOClustering.LoadData(filePath);

if (customers.Count == 0)
{
    Console.WriteLine("No valid customers found.");
    return;
}

// Initialize swarm
var particles = PSOClustering.InitializeParticles(numParticles, numClusters);

// Execute PSO algorithm
var bestParticles = PSOClustering.PSO(particles, numClusters, numIterations);

// Output best clusters
foreach (var particle in bestParticles)
{
    Console.WriteLine($"Best fitness: {particle.Fitness}");
}

public class RFM
{
    public string CustomerID { get; set; }
    public double Recency { get; set; }
    public double Frequency { get; set; }
    public double Monetary { get; set; }
}

public class Particle
{
    public List<RFM> Position { get; set; }  // Position represents the cluster centers (RFM)
    public List<RFM> BestPosition { get; set; }
    public List<double> Velocity { get; set; }
    public double Fitness { get; set; }

    public Particle(int numClusters)
    {
        Position = new List<RFM>(new RFM[numClusters]);
        BestPosition = new List<RFM>(new RFM[numClusters]);
        Velocity = new List<double>(new double[numClusters]);
    }
}

public class PSOClustering
{
    private static List<RFM> customers;

    // Load customer data and calculate RFM features
    public static List<RFM> LoadData(string filePath)
    {
        customers = new List<RFM>();

        // Open and read the Excel file using ExcelDataReader
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        
        using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
        using (var reader = ExcelReaderFactory.CreateReader(stream))
        {
            var result = reader.AsDataSet();
            var table = result.Tables[0]; // Assume the first worksheet

            for (int row = 1; row < table.Rows.Count; row++) // Start from row 1 to skip headers
            {
                var dataRow = table.Rows[row];
                string invoiceNo = dataRow[0].ToString();
                string customerIdStr = dataRow[6].ToString();

                if (string.IsNullOrEmpty(customerIdStr) || invoiceNo.StartsWith("C"))
                {
                    continue; // Skip missing customer IDs or canceled invoices
                }

                DateTime? invoiceDate = GetSafeDateTime(dataRow[3]);
                if (!invoiceDate.HasValue)
                {
                    continue; // Skip invalid dates
                }

                double unitPrice = GetSafeDouble(dataRow[5]);
                double quantity = GetSafeDouble(dataRow[4]);
                double monetaryValue = unitPrice * quantity;

                RFM customerRFM = customers.FirstOrDefault(c => c.CustomerID == customerIdStr);
                if (customerRFM == null)
                {
                    customerRFM = new RFM
                    {
                        CustomerID = customerIdStr,
                        Recency = (DateTime.Now - invoiceDate.Value).TotalDays,
                        Frequency = 1,
                        Monetary = monetaryValue
                    };
                    customers.Add(customerRFM);
                }
                else
                {
                    customerRFM.Frequency += 1;
                    customerRFM.Monetary += monetaryValue;
                }
            }
        }

        return customers;
    }

    // Safe conversion methods
    public static double GetSafeDouble(object value)
    {
        if (double.TryParse(value.ToString(), out double result))
        {
            return result;
        }
        return 0.0;
    }

    public static DateTime? GetSafeDateTime(object value)
    {
        if (DateTime.TryParse(value.ToString(), out DateTime result))
        {
            return result;
        }
        return null;
    }

    // Initialize particles for PSO
    public static List<Particle> InitializeParticles(int numParticles, int numClusters)
    {
        List<Particle> particles = new List<Particle>();
        Random random = new Random();

        for (int i = 0; i < numParticles; i++)
        {
            Particle particle = new Particle(numClusters);
            for (int j = 0; j < numClusters; j++)
            {
                particle.Position[j] = new RFM
                {
                    Recency = random.NextDouble() * 100,
                    Frequency = random.NextDouble() * 10,
                    Monetary = random.NextDouble() * 1000
                };

                particle.BestPosition[j] = new RFM
                {
                    Recency = particle.Position[j].Recency,
                    Frequency = particle.Position[j].Frequency,
                    Monetary = particle.Position[j].Monetary
                };

                particle.Velocity[j] = random.NextDouble();
            }
            particles.Add(particle);
        }

        return particles;
    }

    // PSO algorithm for clustering
    public static List<Particle> PSO(List<Particle> particles, int numClusters, int numIterations)
    {
        double inertiaWeight = 0.5;
        double cognitiveComponent = 1.5;
        double socialComponent = 1.5;
        Random random = new Random();

        // Initialize global best
        Particle globalBest = particles.First();
        globalBest.Fitness = CalculateFitness(globalBest.Position);

        for (int iteration = 0; iteration < numIterations; iteration++)
        {
            foreach (var particle in particles)
            {
                // Update each particle's velocity and position
                for (int i = 0; i < particle.Velocity.Count && i < particle.Position.Count; i++)
                {
                    var currentVelocity = particle.Velocity[i];
                    var cognitive = cognitiveComponent * random.NextDouble() *
                                    (particle.BestPosition[i].Recency - particle.Position[i].Recency);
                    var social = socialComponent * random.NextDouble() *
                                 (globalBest.Position[i].Recency - particle.Position[i].Recency);

                    // Update velocity and position
                    particle.Velocity[i] = inertiaWeight * currentVelocity + cognitive + social;
                    particle.Position[i].Recency += particle.Velocity[i];
                }

                // Update fitness
                particle.Fitness = CalculateFitness(particle.Position);

                // Update personal best
                if (particle.Fitness < CalculateFitness(particle.BestPosition))
                {
                    particle.BestPosition = new List<RFM>(particle.Position);
                }

                // Update global best
                if (particle.Fitness < globalBest.Fitness)
                {
                    globalBest = particle;
                }
            }
        }

        return particles;
    }

    // Calculate fitness based on RFM distances
    public static double CalculateFitness(List<RFM> clusterCenters)
    {
        double fitness = 0;

        foreach (var customer in customers)
        {
            double minDistance = double.MaxValue;

            foreach (var center in clusterCenters)
            {
                double distance = Math.Pow(customer.Recency - center.Recency, 2) +
                                  Math.Pow(customer.Frequency - center.Frequency, 2) +
                                  Math.Pow(customer.Monetary - center.Monetary, 2);

                if (distance < minDistance)
                {
                    minDistance = distance;
                }
            }

            fitness += minDistance;
        }

        return fitness;
    }
}
