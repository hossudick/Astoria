﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SecureSimulator;
using System.IO;
using Microsoft.WindowsAzure.StorageClient;
using Microsoft.WindowsAzure;

namespace CloudLibrary
{
    /// <summary>
    /// functions for converting a destination to text and writing it to a file and pulling it out
    /// </summary>
    public static class ObjectToText
    {
        static char[] space = { ' ' };

        /// <summary>
        /// returns a string of the form:
        /// #BucketTable num.rows num.cols
        /// i j AS1 AS2 AS3 AS4 .
        /// #Best num.rows
        /// i AS1 AS2 AS3
        /// #BestRelation num.rows
        /// byte1 byte2 byte3
        /// 
        /// since we never ended up using L it does not put L in the file
        /// note: we may need L at somepoint in which case it would need to be in the file too.
        /// </summary>
        /// <param name="d"></param>
        /// <returns></returns>
        public static void destinationToText(Destination d, string filename)
        {  
            /*** output
             * i j as1 as2 as3
             * for each non-null element [i,j] in buckettable 
             * ***/
            StreamWriter output = new StreamWriter(filename);
            destinationToStream(d, output);
            output.Close();

        }


        public static void destinationToBlob(Destination d, string containerName,string blobName)
        {
            //set up streamwriter for the blob.
            StreamWriter output =new StreamWriter( BlobLibrary.getBlobWriteStream(containerName,blobName));
            destinationToStream(d, output);
            output.Close();

        }

   
        private static void destinationToStream(Destination d, StreamWriter output)
        {
            output.WriteLine("#destination " + d.destination);
            /*** output
           * i j as1 as2 as3
           * for each non-null element [i,j] in buckettable 
           * ***/
            output.WriteLine("#buckettable " + d.BucketTable.GetLength(0) + " " + d.BucketTable.GetLength(1));
            for (int i = 0; i < d.BucketTable.GetLength(0); i++)
            {
                for (int j = 0; j < d.BucketTable.GetLength(1); j++)
                {
                    if (d.BucketTable[i, j] != null)
                    {
                        output.Write(i + " " + j + " ");
                        for (int k = 0; k < d.BucketTable[i, j].Count; k++)
                            output.Write(d.BucketTable[i, j][k] + " ");
                        output.Write("\n");
                    }
                }
            }
            /** output i as1 as2 as3 
             * for each non-null list in the best[i] array. **/
            output.WriteLine("#best " + d.Best.Length);
            for (int i = 0; i < d.Best.Length; i++)
            {
                if (d.Best[i] != null)
                {
                    output.Write(i + " ");
                    for (int j = 0; j < d.Best[i].Count; j++)
                        output.Write(d.Best[i][j] + " ");
                    output.Write("\n");
                }
            }
            /*** output 1 line with all the values of bestrelation separated by ' ' since
             * it is a simple object **/
            output.WriteLine("#bestrelation " + d.BestRelation.Length);
            for (int i = 0; i < d.BestRelation.Length; i++)
                output.Write(d.BestRelation[i] + " ");
            output.Write("\n");

            output.WriteLine("#L " + d.L.Length);
            for (int i = 0; i < d.L.Length; i++)
                output.Write(d.L[i]+ " ");
            output.Write("\n");


        }

        public static Destination destinationFromText(string filename)
        {    
            StreamReader input = new StreamReader(filename);
            Destination toreturn = destinationFromStream(input);
            input.Close();
            return toreturn;
        }

        public static Destination destinationFromBlob(string containerName,string blobName)
        {
          
            StreamReader input = new StreamReader(BlobLibrary.getBlobReadStream(containerName,blobName));
           Destination toreturn= destinationFromStream(input);
            input.Close();

            return toreturn;
        }

        private static Destination destinationFromStream(StreamReader input)
        {
            
            Destination toreturn = new Destination();
            //some constants to make this a little nicer.
            int currentlyReading = -1;
            const int buckettable = 0;
            const int best = 1;
            const int bestrelation = 2;
            const int l = 3;

            while (!input.EndOfStream)
            {
                string line = input.ReadLine().ToLower();
                string[] pieces = line.Split(space, StringSplitOptions.RemoveEmptyEntries);
                if (line[0] == '#')
                {
                    if (line.IndexOf("buckettable") >= 0)
                    {
                        currentlyReading = buckettable;
                        int numRows = int.Parse(pieces[1]);
                        int numCols = int.Parse(pieces[2]);
                        toreturn.BucketTable = new List<UInt32>[numRows, numCols];

                    }
                    else if (line.IndexOf("bestrelation") >= 0)
                    {
                        currentlyReading = bestrelation;
                        int numRows = int.Parse(pieces[1]);
                        toreturn.BestRelation = new byte[numRows];
                    }
                    else if (line.IndexOf("destination") >= 0)
                    {
                       
                        UInt32 destNum = UInt32.Parse(pieces[1]);
                        toreturn.destination = destNum;
                    }
                    else if (line.IndexOf("best") >= 0)
                    {
                        currentlyReading = best;
                        int numRows = int.Parse(pieces[1]);
                        toreturn.Best = new List<UInt32>[numRows];
                    }
                    else if (line.IndexOf("l") >= 0)
                    {
                        currentlyReading = l;
                        int numRows = int.Parse(pieces[1]);
                        toreturn.L = new byte[numRows];
                    }
                }
                else
                {
                    int row, col;
                    switch (currentlyReading)
                    {
                        case buckettable:
                            row = int.Parse(pieces[0]);
                            col = int.Parse(pieces[1]);
                            toreturn.BucketTable[row, col] = new List<UInt32>();
                            for (int i = 2; i < pieces.Length; i++)
                                toreturn.BucketTable[row, col].Add(UInt32.Parse(pieces[i]));
                            break;
                        case best:
                            row = int.Parse(pieces[0]);
                            toreturn.Best[row] = new List<UInt32>();
                            for (int i = 1; i < pieces.Length; i++)
                                toreturn.Best[row].Add(UInt32.Parse(pieces[i]));
                            break;
                        case bestrelation:
                            for (int i = 0; i < pieces.Length; i++)
                                toreturn.BestRelation[i] = byte.Parse(pieces[i]);
                            break;
                        case l:
                            for (int i = 0; i < pieces.Length; i++)
                                toreturn.L[i] = byte.Parse(pieces[i]);
                            break;
                    }
                }
            }
            //rehydrate the parts of the object not passed in text.
            toreturn.ChosenParent = new UInt32[toreturn.Best.Length];
            toreturn.ChosenPath = new List<UInt32>[toreturn.Best.Length];
          
            toreturn.SecP = new bool[toreturn.Best.Length];
            toreturn.U = new Int32[toreturn.Best.Length];
            toreturn.W = new short[toreturn.Best.Length];
            for (int i = 0; i < toreturn.W.Length; i++)
                toreturn.W[i] = 1;
            //init the destination path to itself to kick things off
            toreturn.SecP[toreturn.destination] = true;
            toreturn.ChosenPath[toreturn.destination] = new List<UInt32>();
            toreturn.ChosenPath[toreturn.destination].Add(toreturn.destination);
            toreturn.ChosenParent[toreturn.destination]=toreturn.destination;
            return toreturn;
        }

        public static void messageToBlob(Message m, string containerName, string blobName)
        {
            StreamWriter output = new StreamWriter(BlobLibrary.getBlobWriteStream(containerName, blobName));
            for (int i = 0; i < m.UBefore.Length; i++)
                output.Write(m.UBefore[i]+ " ");
            output.Write("\n");
            for (int i = 0; i < m.UAfter.Length; i++)
                output.Write(m.UAfter[i] + " ");
            output.Write("\n");
            output.Close();
        }

        public static Message messageFromBlob(string containerName, string blobName)
        {
            
            StreamReader input = new StreamReader(BlobLibrary.getBlobReadStream(containerName, blobName));
            string[] UBeforePieces = input.ReadLine().Split(space, StringSplitOptions.RemoveEmptyEntries);
            string[] UAfterPieces = input.ReadLine().Split(space, StringSplitOptions.RemoveEmptyEntries);
            Message toreturn = new Message();
            toreturn.UBefore = new Int32[UBeforePieces.Length];
            toreturn.UAfter = new Int32[UAfterPieces.Length];
            for (int i = 0; i < toreturn.UBefore.Length; i++)
                toreturn.UBefore[i] = Int32.Parse(UBeforePieces[i]);
            for (int i = 0; i < toreturn.UAfter.Length; i++)
                toreturn.UAfter[i] = Int32.Parse(UAfterPieces[i]);


            input.Close();
            return toreturn;
        }

        public static void SToBlob(bool[] S, string containerName, string blobName)
        {
            StreamWriter output = new StreamWriter(BlobLibrary.getBlobWriteStream(containerName, blobName));
            for (int i = 0; i < S.Length; i++)
                output.Write(S[i] + " ");
            output.Write("\n");
            output.Close();
        }

        public static bool[] SFromBlob(string containerName, string blobName)
        {
          
            StreamReader input = new StreamReader(BlobLibrary.getBlobReadStream(containerName, blobName));
            string[] pieces = input.ReadLine().Split(space, StringSplitOptions.RemoveEmptyEntries);
            bool[] toreturn = new bool[pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
                toreturn[i] = bool.Parse(pieces[i]);
            input.Close();
            return toreturn;
        }
    }
}
