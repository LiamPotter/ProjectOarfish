using System;

namespace Utils
{
	[Serializable]
	public class FlipActionBool
	{
		public bool Value
		{
			get => m_currentValue;
			set
			{
				if (m_currentValue == value)
				{
					return;
				}
				
				if (m_currentValue != value)
				{
					FlipDetected?.Invoke(value);
				}

				m_currentValue = value;
			}
		}

		private bool m_currentValue;

		public Action<bool> FlipDetected;

		public FlipActionBool(bool startValue,Action<bool> flipDetected)
		{
			m_currentValue = startValue;
			FlipDetected = flipDetected;
		}

		public static implicit operator bool(FlipActionBool flipActionBool) => flipActionBool?.Value ?? false;
	}
}