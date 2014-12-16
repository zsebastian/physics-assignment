using UnityEngine;
using System.Collections;

namespace Pool
{
	/* Physics from:
	 * 	http://web.stanford.edu/group/billiards/PoolPhysicsSimulationByEventPrediction1MotionTransitions.pdf
	 */
	public class PhysicsModel : MonoBehaviour 
	{
		private enum State {Still, Sliding, Rolling, Spinning };

		private State m_State;

		public bool Still {get; private set;}
		public bool precise = false;

		public float m_ForwardAngle;
		private float m_Mass = 0.15f;
		private float m_Radius;
		private float m_Force;
		private Vector3 m_Velocity;
		private Vector3 m_AngularVelocity;
		private float m_Inertia;
		private float m_BufferedDelta;
		private const float m_TimeStep = 0.001f;
		private const float g = 9.82f;
		private float m_Angle;
		private Vector3 m_RelativeVelocity;
		private float m_Time;
		private float m_FirstDuration;
		private float m_StaticFrictionCoefficient = 0.2f;
		private float m_RollingFrictionCoefficient = 0.015f;
		private Vector3 m_EndPos;
		private bool m_Sliding = false;
		private bool m_Rolling = false;
		private float m_FirstRollingDuration;

		private Vector3 m_FirstVelocity;
		private Vector3 m_FirstRelativeVelocity;
		private Vector3 m_FirstAngularVelocity;

		void Start()
		{
			Still = true;
			m_Radius = 0.056f;
			m_Inertia = (2.0f/5.0f) * m_Mass * m_Radius * m_Radius;
		}

		Vector3 Rotate2D(Vector3 v, float angle)
		{
			float[,] tMatrix = new float[2, 2]
			{ 	
				{ Mathf.Cos(angle), - Mathf.Sin(angle)},
				{ Mathf.Sin(angle), Mathf.Cos(angle) }
			};
			
			return new Vector3(tMatrix[0, 0] * v.x + tMatrix[0, 1] * v.z,
			                   0,  
			                   tMatrix[1, 0] * v.x + tMatrix[1, 1] * v.z);
		}

		Vector3 Rotate2D(Vector2 v, float angle)
		{
			float[,] tMatrix = new float[2, 2]
			{ 	
				{ Mathf.Cos(angle), - Mathf.Sin(angle)},
				{ Mathf.Sin(angle), Mathf.Cos(angle) }
			};
			
			return new Vector2(tMatrix[0, 0] * v.x + tMatrix[0, 1] * v.y, 
			                   tMatrix[1, 0] * v.x + tMatrix[1, 1] * v.y);
		}

		public void Strike(Main.CueInfo info)
		{
			float force;
			float a = info.position.x;
			float b = info.position.y;
			
			float aSqr = a * a;
			float bSqr = b * b;

			float c = Mathf.Abs(Mathf.Sqrt(Mathf.Pow(m_Radius, 2) - aSqr - bSqr));
			float cSqr = c * c;

			m_ForwardAngle = info.forwardAngle;
		
			force = 2 * info.mass * info.cueVelocity;

			force = force / (1 + (m_Mass / info.mass) + (5 / 2 * m_Radius)
				* (aSqr
				   + (bSqr * Mathf.Pow(Mathf.Cos(info.angle), 2)) 
				   + (cSqr * Mathf.Pow(Mathf.Sin(info.angle), 2))
				   - (2 * b * c * Mathf.Cos(info.angle) * Mathf.Sin(info.angle))
				   ));

			float ax = -c * force * Mathf.Sin(info.angle) + b * force * Mathf.Cos(info.angle);
			float ay =  a * force * Mathf.Sin(info.angle);
			float az = -a * force * Mathf.Cos(info.angle);

			m_AngularVelocity = (1.0f / m_Inertia) * (new Vector3(ax, ay, az));

			m_Force = force;
			m_Velocity = new Vector3(0, (-m_Force / m_Mass) * Mathf.Cos(info.angle), /*0.0f Assumed to be zero.*/ (-m_Force / m_Mass) * Mathf.Sin(info.angle));
			m_RelativeVelocity = m_Velocity + Vector3.Cross(m_Radius * Vector3.up, m_AngularVelocity);

			Debug.Log("Force: " + force);
			Debug.Log(string.Format("Velocity: {0}", m_Velocity));
			Debug.Log(string.Format("Angular Velocity: {0}", m_AngularVelocity));
			m_FirstDuration = (2.0f * m_RelativeVelocity.magnitude) / (7.0f * m_StaticFrictionCoefficient * g);
			m_Angle = info.angle;

			m_EndPos.x = m_Velocity.magnitude * m_FirstDuration 
				- ((1.0f / 2.0f) * m_StaticFrictionCoefficient * g * m_FirstDuration * m_FirstDuration * m_RelativeVelocity.normalized.x);

			m_EndPos.z = 
				+ ((1.0f / 2.0f) * m_StaticFrictionCoefficient * g * m_FirstDuration * m_FirstDuration * m_RelativeVelocity.normalized.y);

			m_EndPos = transform.position - Rotate2D(m_EndPos, m_ForwardAngle);

			m_Sliding = true;
			m_Rolling = false;

			Still = false;
			m_State = State.Sliding;

			m_FirstVelocity = m_Velocity;
			m_FirstRelativeVelocity = m_RelativeVelocity;
			m_FirstAngularVelocity = m_AngularVelocity;
		}

		void Update()
		{
			m_BufferedDelta += Time.deltaTime * 1f;
			if (Still)
			{
				m_BufferedDelta = 0.0f;
			}
			else
			{
				for (;m_BufferedDelta > m_TimeStep; m_BufferedDelta -= m_TimeStep)
				{
					m_Time += m_TimeStep;
					Integrate();
				}
			}
		}

		void Integrate()
		{
			if (precise)
			{
				transform.position = AdvancePrecisePosition(transform.position, m_Time, m_TimeStep);
			}
			else
			{
				float sfc = m_StaticFrictionCoefficient;
				float rfc = m_RollingFrictionCoefficient;

				if (m_State == State.Sliding)
				{
					var diffVRel = (7.0f / 2.0f) * sfc * g * m_TimeStep * m_RelativeVelocity.normalized;
					if (diffVRel.magnitude > m_RelativeVelocity.magnitude)
					{
						m_State = State.Rolling;
					}

					m_RelativeVelocity -= (7.0f / 2.0f) * sfc * g * m_TimeStep * m_RelativeVelocity.normalized;
					m_AngularVelocity -= (((5.0f * sfc * g) / (2.0f * m_Radius)) * m_TimeStep) * Vector3.Cross(Vector3.up, m_RelativeVelocity.normalized);
					m_Velocity -= sfc * g * m_TimeStep * m_RelativeVelocity.normalized;

					Vector2 pos = Vector2.zero;
					pos.x = -m_Velocity.y * m_TimeStep;
					pos.y = m_Velocity.x * m_TimeStep;
					pos = Rotate2D(pos, m_ForwardAngle);
					transform.position = transform.position - new Vector3(pos.x, 0, pos.y);
				}
				else
				{
					Vector3 diff = rfc * g * m_TimeStep * m_Velocity.normalized;

					if (diff.magnitude > m_Velocity.magnitude)
					{
						m_State = State.Still;
						Still = true;
					}
					m_Velocity -= diff;

					Debug.Log(m_Velocity);
					Vector2 pos = Vector2.zero;
					pos.x = -m_Velocity.y * m_TimeStep;
					pos.y = m_Velocity.x * m_TimeStep;
					pos = Rotate2D(pos, m_ForwardAngle);
					transform.position = transform.position - new Vector3(pos.x, 0, pos.y);

					m_AngularVelocity = m_AngularVelocity.normalized * (m_Velocity.magnitude / m_Radius);
				}
			}
		}

		private Vector3 AdvancePrecisePosition(Vector3 currentPos, float time, float deltaTime)
		{
			Vector3 pos = Vector3.zero;
			Vector3 vel = CalculateVelocity(time);
			pos.x = -vel.y * deltaTime;
			pos.z = vel.x * deltaTime;
			return currentPos - Rotate2D(pos, m_ForwardAngle);
		}

		private Vector3 CalculateAngularVelocity(float time)
		{
			float sfc = m_StaticFrictionCoefficient;

			if (CalculateIsSliding(time))
			{
				return m_FirstAngularVelocity 
					- (((5.0f * sfc * g) / (2.0f * m_Radius)) 
					   * time) * Vector3.Cross(Vector3.up, CalculateRelativeVelocity(0f).normalized);
			}
			else
			{
				float slide = CalculateSlidingDuration(0f);

				Vector3 angular = m_FirstAngularVelocity 
				- (((5.0f * sfc * g) / (2.0f * m_Radius)) 
					   * slide) * Vector3.Cross(Vector3.up, CalculateRelativeVelocity(slide).normalized);

				Vector3 angularNormal = angular.normalized;
				return angularNormal * (CalculateVelocity(time).magnitude / m_Radius);
			}
		}

		private Vector3 CalculateRelativeVelocity(float time)
		{
			float sfc = m_StaticFrictionCoefficient;
			float rfc = m_RollingFrictionCoefficient;
			
			return m_FirstRelativeVelocity - (7.0f / 2.0f) * sfc * g * time * m_FirstRelativeVelocity.normalized;
		}

		private Vector3 CalculateVelocity(float time)
		{
			float sfc = m_StaticFrictionCoefficient;
			float rfc = m_RollingFrictionCoefficient;

			if (CalculateIsSliding(time))
			{
				return m_Velocity - sfc * g * time * CalculateRelativeVelocity(0f).normalized;
			}
			else
			{
				float sliding = CalculateSlidingDuration(0f);
				Vector3 vel = m_FirstVelocity - sfc * g * sliding * CalculateRelativeVelocity(0f).normalized;
				return vel - rfc * g * (sliding - time) * m_Velocity.normalized;;
			}

		}


		private float CalculateSlidingDuration(float time)
		{
			float sfc = m_StaticFrictionCoefficient;

			return (2.0f * CalculateRelativeVelocity(time).magnitude) / (7.0f * sfc * g);
		}

		private bool CalculateIsSliding(float time)
		{
			float slidingDuration = CalculateSlidingDuration(0f);
			return time < slidingDuration;
		}

		private bool CalculateIsRolling(float time)
		{
			return !CalculateIsSliding(time);
		}

		public void Collide(Vector3 otherPosition, Vector3 collisionNormal, Vector3 otherAngularVelocity, Vector3 otherVelocity)
		{
			Debug.Log("Collide");
		}

		void OnGUI()
		{

		}
	}
}

