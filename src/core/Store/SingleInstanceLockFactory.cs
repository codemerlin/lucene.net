/* 
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 * 
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;

namespace Lucene.Net.Store
{

    /// <summary> Implements <see cref="LockFactory" /> for a single in-process instance,
    /// meaning all locking will take place through this one instance.
    /// Only use this <see cref="LockFactory" /> when you are certain all
    /// IndexReaders and IndexWriters for a given index are running
    /// against a single shared in-process Directory instance.  This is
    /// currently the default locking for RAMDirectory.
    /// 
    /// </summary>
    /// <seealso cref="LockFactory">
    /// </seealso>

    public class SingleInstanceLockFactory : LockFactory
    {
        private HashSet<string> locks = new HashSet<string>();

        public override Lock MakeLock(String lockName)
        {
            // We do not use the LockPrefix at all, because the private
            // HashSet instance effectively scopes the locking to this
            // single Directory instance.
            return new SingleInstanceLock(locks, lockName);
        }

        public override void ClearLock(String lockName)
        {
            lock (locks)
            {
                if (locks.Contains(lockName))
                {
                    locks.Remove(lockName);
                }
            }
        }
    }


    class SingleInstanceLock : Lock
    {

        internal String lockName;
        private HashSet<string> locks;

        public SingleInstanceLock(HashSet<string> locks, String lockName)
        {
            this.locks = locks;
            this.lockName = lockName;
        }

        public override bool Obtain()
        {
            lock (locks)
            {
                if (locks.Contains(lockName) == false)
                {
                    locks.Add(lockName);
                    return true;
                }

                return false;
            }
        }

        public override void Dispose()
        {
            lock (locks)
            {
                locks.Remove(lockName);
            }
        }

        public override bool IsLocked()
        {
            lock (locks)
            {
                return locks.Contains(lockName);
            }
        }

        public override String ToString()
        {
            return base.ToString() + ": " + lockName;
        }
    }
}