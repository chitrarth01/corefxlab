﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using Microsoft.Xunit.Performance;
using System.Security.Cryptography;
using Xunit;

namespace System.IO.Compression.Tests
{
    public class BrotliPerfomanceTests
    {
        public static string GetTestFilePath(int? index = null, string memberName = null, int lineNumber = 0)
        {
            return Path.Combine(Path.GetTempPath(), string.Format(
                index.HasValue ? "{0}_{1}_{2}_{3}" : "{0}_{1}_{2}",
                memberName ?? "TestBase", lineNumber, Path.GetRandomFileName(),
                index.GetValueOrDefault()));
        }

        private static string CreateCompressedFile(CompressionType type)
        {
            const int fileSize = 1000000;
            string filePath = GetTestFilePath() + ".br";
            switch (type)
            {
                case CompressionType.CryptoRandom:
                    using (RandomNumberGenerator rand = RandomNumberGenerator.Create())
                    {
                        byte[] bytes = new byte[fileSize];
                        rand.GetBytes(bytes);
                        using (FileStream output = File.Create(filePath))
                        using (BrotliStream zip = new BrotliStream(output, CompressionMode.Compress))
                            zip.Write(bytes, 0, bytes.Length);
                    }
                    break;
                case CompressionType.RepeatedSegments:
                    {
                        byte[] bytes = new byte[fileSize / 1000];
                        new Random(128453).NextBytes(bytes);
                        using (FileStream output = File.Create(filePath))
                        using (BrotliStream zip = new BrotliStream(output, CompressionMode.Compress))
                            for (int i = 0; i < 1000; i++)
                                zip.Write(bytes, 0, bytes.Length);
                    }
                    break;
                case CompressionType.NormalData:
                    {
                        byte[] bytes = new byte[fileSize];
                        new Random(128453).NextBytes(bytes);
                        using (FileStream output = File.Create(filePath))
                        using (BrotliStream zip = new BrotliStream(output, CompressionMode.Compress))
                            zip.Write(bytes, 0, bytes.Length);
                    }
                    break;
            }
            return filePath;
        }

        private static byte[] CreateBytesToCompress(CompressionType type)
        {
            const int fileSize = 500000;
            byte[] bytes = new byte[fileSize];
            switch (type)
            {
                case CompressionType.CryptoRandom:
                    using (RandomNumberGenerator rand = RandomNumberGenerator.Create())
                        rand.GetBytes(bytes);
                    break;
                case CompressionType.RepeatedSegments:
                    {
                        byte[] small = new byte[1000];
                        new Random(123453).NextBytes(small);
                        for (int i = 0; i < fileSize / 1000; i++)
                        {
                            small.CopyTo(bytes, 1000 * i);
                        }
                    }
                    break;
                case CompressionType.VeryRepetitive:
                    {
                        byte[] small = new byte[100];
                        new Random(123453).NextBytes(small);
                        for (int i = 0; i < fileSize / 100; i++)
                        {
                            small.CopyTo(bytes, 100 * i);
                        }
                        break;
                    }
                case CompressionType.NormalData:
                    new Random(123453).NextBytes(bytes);
                    break;
            }
            return bytes;
        }

        public enum CompressionType
        {
            CryptoRandom,
            RepeatedSegments,
            VeryRepetitive,
            NormalData
        }

        private const int Iter = 1;

        [Benchmark(InnerIterationCount = Iter)]
        [InlineData(CompressionType.CryptoRandom)]
        [InlineData(CompressionType.RepeatedSegments)]
        [InlineData(CompressionType.NormalData)]
        public void DecompressUsingStream(CompressionType type)
        {
            string testFilePath = CreateCompressedFile(type);
            int bufferSize = 1024;
            var bytes = new byte[bufferSize];
            using (MemoryStream brStream = new MemoryStream(File.ReadAllBytes(testFilePath)))
                foreach (var iteration in Benchmark.Iterations)
                    using (iteration.StartMeasurement())
                        for (int i = 0; i < Benchmark.InnerIterationCount; i++)
                        {
                            int retCount = -1;
                            using (BrotliStream brotliDecompressStream = new BrotliStream(brStream, CompressionMode.Decompress, true))
                            {
                                while (retCount != 0)
                                {
                                    retCount = brotliDecompressStream.Read(bytes, 0, bufferSize);
                                }
                            }
                            brStream.Seek(0, SeekOrigin.Begin);
                        }
            File.Delete(testFilePath);
        }

        [Benchmark]
        [InlineData(CompressionType.CryptoRandom)]
        [InlineData(CompressionType.RepeatedSegments)]
        [InlineData(CompressionType.VeryRepetitive)]
        [InlineData(CompressionType.NormalData)]
        public void CompressUsingStream(CompressionType type)
        {
            byte[] bytes = CreateBytesToCompress(type);
            foreach (var iteration in Benchmark.Iterations)
            {
                string filePath = GetTestFilePath();
                FileStream output = File.Create(filePath);
                using (BrotliStream brotliCompressStream = new BrotliStream(output, CompressionMode.Compress))
                {
                    using (iteration.StartMeasurement())
                    {
                        brotliCompressStream.Write(bytes, 0, bytes.Length);
                    }
                }
                File.Delete(filePath);
            }
        }

        [Benchmark(InnerIterationCount = Iter)]
        [InlineData(CompressionType.CryptoRandom)]
        [InlineData(CompressionType.RepeatedSegments)]
        [InlineData(CompressionType.NormalData)]
        public void Decompress(CompressionType type)
        {
            string testFilePath = CreateCompressedFile(type);
            int bufferSize = 1000000;
            byte[] data = File.ReadAllBytes(testFilePath);
            var bytes = new byte[bufferSize];
            foreach (var iteration in Benchmark.Iterations)
                using (iteration.StartMeasurement())
                    for (int i = 0; i < Benchmark.InnerIterationCount; i++)
                        Brotli.Decompress(data, bytes, out int consumed, out int written);
            File.Delete(testFilePath);
        }

        [Benchmark]
        [InlineData(CompressionType.CryptoRandom)]
        [InlineData(CompressionType.RepeatedSegments)]
        [InlineData(CompressionType.VeryRepetitive)]
        [InlineData(CompressionType.NormalData)]
        public void Compress(CompressionType type)
        {
            byte[] bytes = CreateBytesToCompress(type);
            foreach (var iteration in Benchmark.Iterations)
            {
                byte[] compressed = new byte[bytes.Length];
                using (iteration.StartMeasurement())
                {
                    Brotli.Compress(bytes, compressed, out int consumed, out int writen);
                }
            }
        }
    }
}
