﻿/* Copyright 2015-present MongoDB Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
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
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver.Core.Misc;

namespace MongoDB.Driver.Core.Operations
{
    internal class AsyncCursorEnumerator<TDocument> :
#if NETSTANDARD2_1_OR_GREATER
        IAsyncEnumerator<TDocument>,
#endif
        IEnumerator<TDocument>
    {
        // private fields
        private IEnumerator<TDocument> _batchEnumerator;
        private readonly CancellationToken _cancellationToken;
#if NETSTANDARD2_1_OR_GREATER
        private IAsyncCursor<TDocument> _cursor;
        private readonly Task<IAsyncCursor<TDocument>> _cursorTask;
#else
        private readonly IAsyncCursor<TDocument> _cursor;
#endif
        private bool _disposed;
        private bool _finished;
        private bool _started;

        // constructors
        public AsyncCursorEnumerator(IAsyncCursor<TDocument> cursor, CancellationToken cancellationToken)
        {
            _cursor = Ensure.IsNotNull(cursor, nameof(cursor));
            _cancellationToken = cancellationToken;
        }

#if NETSTANDARD2_1_OR_GREATER
        public AsyncCursorEnumerator(Task<IAsyncCursor<TDocument>> cursorTask, CancellationToken cancellationToken)
        {
            _cursorTask = Ensure.IsNotNull(cursorTask, nameof(cursorTask));
            _cancellationToken = cancellationToken;
        }
#endif

        // public properties
        public TDocument Current
        {
            get
            {
                ThrowIfDisposed();
                if (!_started)
                {
                    throw new InvalidOperationException("Enumeration has not started. Call MoveNext.");
                }
                if (_finished)
                {
                    throw new InvalidOperationException("Enumeration already finished.");
                }
                return _batchEnumerator.Current;
            }
        }

        object IEnumerator.Current
        {
            get { return Current; }
        }

        // public properties
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _batchEnumerator?.Dispose();
                _cursor?.Dispose();
            }
        }

#if NETSTANDARD2_1_OR_GREATER
        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                if (_cursor == null)
                {
                    _cursor = await _cursorTask.ConfigureAwait(false);
                }
                _cursorTask?.Dispose();
                Dispose();
            }
        }
#endif

        public bool MoveNext()
        {
            ThrowIfDisposed();
            _started = true;

            if (_batchEnumerator != null && _batchEnumerator.MoveNext())
            {
                return true;
            }

            while (true)
            {
                if (_cursor.MoveNext(_cancellationToken))
                {
                    _batchEnumerator?.Dispose();
                    _batchEnumerator = _cursor.Current.GetEnumerator();
                    if (_batchEnumerator.MoveNext())
                    {
                        return true;
                    }
                }
                else
                {
                    _batchEnumerator = null;
                    _finished = true;
                    return false;
                }
            }
        }

#if NETSTANDARD2_1_OR_GREATER
        public async ValueTask<bool> MoveNextAsync()
        {
            ThrowIfDisposed();
            _started = true;

            if (_batchEnumerator != null && _batchEnumerator.MoveNext())
            {
                return true;
            }

            if (_cursor == null)
            {
                _cursor = await _cursorTask.ConfigureAwait(false);
            }

            while (true)
            {
                if (await _cursor.MoveNextAsync(_cancellationToken).ConfigureAwait(false))
                {
                    _batchEnumerator?.Dispose();
                    _batchEnumerator = _cursor.Current.GetEnumerator();
                    if (_batchEnumerator.MoveNext())
                    {
                        return true;
                    }
                }
                else
                {
                    _batchEnumerator = null;
                    _finished = true;
                    return false;
                }
            }
        }
#endif

        public void Reset()
        {
            ThrowIfDisposed();
            throw new NotSupportedException();
        }

        // private methods
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}
