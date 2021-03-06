﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KnightElfLibrary
{
    public class EventQueue
    {
        Queue<InputMessage> queue;
        bool discard = false;

        public EventQueue()
        {
            queue = new Queue<InputMessage>();
        }

        ~EventQueue()
        {
            queue.Clear();
        }

        public void put(InputMessage input)
        {
            lock (queue)
            {
                if (discard)
                    return;
                // Add the item to the queue
                queue.Enqueue(input);
                // Notify possible getters
                Monitor.Pulse(queue);
            }
        }

        public InputMessage get()
        {
            lock (queue)
            {
                while (true)
                {
                    if (queue.Count > 0)
                    {
                        return queue.Dequeue();
                    }
                    // Wait for a Put to notify us
                    Monitor.Wait(queue);
                }
            }
        }

        public void Disable()
        {
            lock(queue)
            {
                discard = true;
            }
        }

        public void Enable()
        {
            lock (queue)
            {
                discard = false;
            }
        }

        public bool isEnabled()
        {
            lock(queue)
            {
                return !discard;
            }
        }

        public void Clear()
        {
            lock (queue)
            {
                queue.Clear();
            }
        }

        public void ClearAndClose()
        {
            lock (queue)
            {
                // Empty the queue
                queue.Clear();
                // Add a Closed message
                InputMessage msg = new InputMessage();
                msg.CurrentConnectionState = State.Closed;
                queue.Enqueue(msg);
                // Notify getters
                Monitor.Pulse(queue);
            }
        }

        public void ClearAndSuspend()
        {
            lock (queue)
            {
                // Empty the queue
                queue.Clear();
                // Add a Suspended message
                InputMessage msg = new InputMessage();
                msg.CurrentConnectionState = State.Suspended;
                queue.Enqueue(msg);
                // Notify getters
                Monitor.Pulse(queue);
            }
        }
    }
}
