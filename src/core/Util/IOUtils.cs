﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Lucene.Net.Util
{
    public static class IOUtils
    {
        public static readonly string UTF_8 = "UTF-8";

        public static readonly Encoding CHARSET_UTF_8 = Encoding.UTF8;

        public static void CloseWhileHandlingException<E>(E priorException, params IDisposable[] objects)
            where E : Exception
        {
            // java version has a separate implementation here, but might as well re-use the other one until we can't
            CloseWhileHandlingException<E>(priorException, (IEnumerable<IDisposable>)objects);
        }

        public static void CloseWhileHandlingException<E>(E priorException, IEnumerable<IDisposable> objects)
            where E : Exception
        {
            Exception ex = null;

            foreach (IDisposable obj in objects)
            {
                try
                {
                    if (obj != null)
                    {
                        obj.Dispose();
                    }
                }
                catch (Exception e)
                {
                    AddSuppressed((priorException == null) ? ex : priorException, e);
                    if (ex == null)
                    {
                        ex = e;
                    }
                }
            }

            if (priorException != null)
            {
                throw priorException;
            }
            else if (ex != null)
            {
                if (ex is IOException) throw (IOException)ex;
                //if (ex is RuntimeException) throw (RuntimeException)ex;
                //if (ex is Error) throw (Error)ex;

                throw ex;
            }
        }

        public static void Close(params IDisposable[] objects)
        {
            // java version has a separate implementation here, but might as well re-use the other one until we can't
            
            Close((IEnumerable<IDisposable>)objects);
        }

        public static void Close(IEnumerable<IDisposable> objects)
        {
            Exception ex = null;

            foreach (IDisposable obj in objects)
            {
                try
                {
                    if (obj != null)
                    {
                        obj.Dispose();
                    }
                }
                catch (Exception e)
                {
                    AddSuppressed(ex, e);
                    if (ex == null)
                    {
                        ex = e;
                    }
                }
            }

            if (ex != null)
            {
                if (ex is IOException) throw (IOException)ex;
                //if (ex instanceof RuntimeException) throw (RuntimeException) ex;
                //if (ex instanceof Error) throw (Error) ex;
                throw ex;
            }
        }

        public static void CloseWhileHandlingException(params IDisposable[] objects)
        {
            // java version has a separate implementation here, but might as well re-use the other one until we can't
            
            CloseWhileHandlingException((IEnumerable<IDisposable>)objects);
        }

        public static void CloseWhileHandlingException(IEnumerable<IDisposable> objects)
        {
            foreach (IDisposable obj in objects)
            {
                try
                {
                    if (obj != null)
                    {
                        obj.Dispose();
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        //private static readonly MethodInfo SUPPRESS_METHOD;
        static IOUtils()
        {
            // Java version sets suppress method here, not sure if needed for .NET
        }

        public static void AddSuppressed(Exception exception, Exception suppressed)
        {
            // noop in .NET?
        }

        public static TextReader GetDecodingReader(Stream stream, Encoding charSet)
        {
            return new StreamReader(new BufferedStream(stream), charSet);
        }

        public static TextReader GetDecodingReader(FileInfo file, Encoding charSet)
        {
            FileStream stream = null;
            bool success = false;

            try
            {
                stream = file.OpenRead();

                TextReader reader = GetDecodingReader(stream, charSet);
                success = true;
                return reader;
            }
            finally
            {
                if (!success) 
                {
                    IOUtils.Close(stream);
                }
            }            
        }

        public static TextReader GetDecodingReader(Type clazz, string resource, Encoding charSet)
        {
            Stream stream = null;
            bool success = false;

            try
            {
                stream = clazz.Assembly.GetManifestResourceStream(resource);
                TextReader reader = GetDecodingReader(stream, charSet);
                success = true;
                return reader;
            }
            finally
            {
                if (!success)
                {
                    IOUtils.Close(stream);
                }
            }
        }

        public static void DeleteFilesIgnoringExceptions(Lucene.Net.Store.Directory dir, params string[] files)
        {
            foreach (string name in files)
            {
                try
                {
                    dir.DeleteFile(name);
                }
                catch (Exception)
                {
                    // ignore
                }
            }
        }

        public static void Copy(FileInfo source, FileInfo target)
        {
            FileStream fis = null;
            FileStream fos = null;
            try
            {
                fis = source.OpenRead();
                fos = source.OpenWrite();

                byte[] buffer = new byte[1024 * 8];
                int len;

                // TODO: not a clean port of Java code, verify logic
                while ((len = fis.Read(buffer, 0, 1024 * 8)) > 0)
                {
                    fos.Write(buffer, 0, len);
                }
            }
            finally
            {
                Close(fis, fos);
            }
        }
		public static void ReThrow(Exception th)
		{
			if (th != null)
			{
			    var exception = th as IOException;
			    if (exception != null)
				{
					throw exception;
				}
			}
		}
		
		public static void Fsync(FileInfo fileToSync, bool isDir)
		{
			IOException exc = null;
			// If the file is a directory we have to open read-only, for regular files we must open r/w for the fsync to have an effect.
			// See http://blog.httrack.com/blog/2013/11/15/everything-you-always-wanted-to-know-about-fsync/
			try
			{
			    var file = isDir ? fileToSync.OpenRead() : fileToSync.OpenWrite();
			    for (int retry = 0; retry < 5; retry++)
				{

					try
					{
						file.Flush();
						return;
					}
					catch (IOException ioe)
					{
						if (exc == null)
						{
							exc = ioe;
						}
						try
						{
							// Pause 5 msec
							Thread.Sleep(5);
						}
						catch (Exception ie)
						{
							ThreadInterruptedException ex = new ThreadInterruptedException("File sync interrupted",ie);
							AddSuppressed(ex,exc);
							throw ex;
						}
					}
				}
			}
			catch (IOException ioe)
			{
				if (exc == null)
				{
					exc = ioe;
				}
			}
			if (isDir)
			{
				//HM:revisit 
				//assert (Constants.LINUX || Constants.MAC_OS_X) == false :
				// Ignore exception if it is a directory
				return;
			}
			// Throw original exception
			throw exc;
		}
    }
}
