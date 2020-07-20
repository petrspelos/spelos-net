using System;

namespace SpelosNet.Infrastructure
{
    public class InMemoryCache<T>
    {
        private T _value;
        private Func<T> _getFunction;
        private TimeSpan _expirationTime;
        private DateTime _lastGetDateTime;

        public InMemoryCache(Func<T> getFunction, TimeSpan expirationTime)
        {
            _getFunction = getFunction;
            _expirationTime = expirationTime;
            _value = getFunction.Invoke();
            _lastGetDateTime = DateTime.Now;
        }

        public T GetValue()
        {
            if((DateTime.Now - _lastGetDateTime) > _expirationTime)
                _value = _getFunction.Invoke();
            
            return _value;
        }
    }
}
