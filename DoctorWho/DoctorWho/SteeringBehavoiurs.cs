﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework.Net;
using Microsoft.Xna.Framework.Storage;


namespace DoctorWho
{
    class SteeringBehaviours
    {
        Fighter fighter;
        Vector3 wanderTarget;
        int flags;
        Sphere sphere;
        float maxForce = 10.0f;
        public enum CalculationMethods { WeightedTruncatedSum, WeightedTruncatedRunningSumWithPrioritisation, PrioritisedDithering };
        CalculationMethods calculationMethod;

        private Dictionary<behaviour_type, float> weights = new Dictionary<behaviour_type,float>();

        public enum behaviour_type
        {
            none = 0x00000,
            seek = 0x00002,
            flee = 0x00004,
            arrive = 0x00008,
            wander = 0x00010,
            cohesion = 0x00020,
            separation = 0x00040,
            allignment = 0x00080,
            obstacle_avoidance = 0x00100,
            wall_avoidance = 0x00200,
            follow_path = 0x00400,
            pursuit = 0x00800,
            evade = 0x01000,
            interpose = 0x02000,
            hide = 0x04000,
            flock = 0x08000,
            offset_pursuit = 0x10000,
        };

        Random random = new Random();

        public float timeDelta;

        public bool isOn(behaviour_type behaviour)
        {
            return ((flags & (int)behaviour) == (int)behaviour);
        }

        public void turnOn(behaviour_type behaviour)
        {
            flags |= ((int)behaviour);
        }

        public void turnOffAll()
        {
            flags = (int) SteeringBehaviours.behaviour_type.none;
        }

        public SteeringBehaviours(Fighter entity)
        {
            this.fighter = entity;
            calculationMethod = CalculationMethods.WeightedTruncatedRunningSumWithPrioritisation;
            sphere = new Sphere(0.2f);
            XNAGame.Instance().Children.Add(sphere);
            wanderTarget = new Vector3(randomClamped(), randomClamped(), randomClamped());
            wanderTarget.Normalize();
            
            weights.Add(behaviour_type.allignment, 1.0f);
            weights.Add(behaviour_type.cohesion, 2.0f);
            weights.Add(behaviour_type.obstacle_avoidance, 20.0f);
            weights.Add(behaviour_type.wall_avoidance, 20.0f);
            weights.Add(behaviour_type.wander, 1.0f);
            weights.Add(behaviour_type.seek, 1.0f);
            weights.Add(behaviour_type.flee, 1.0f);
            weights.Add(behaviour_type.arrive, 1.0f);
            weights.Add(behaviour_type.pursuit, 1.0f);
            weights.Add(behaviour_type.offset_pursuit, 1.0f);
            weights.Add(behaviour_type.interpose, 1.0f);
            weights.Add(behaviour_type.hide, 1.0f);
            weights.Add(behaviour_type.evade, 0.01f);
            weights.Add(behaviour_type.follow_path, 1.0f);
            weights.Add(behaviour_type.separation, 1.0f);

        }

        Vector3 evade()
        {            
            return Vector3.Zero;
        }

        Vector3 obstacleAvoidance()
        {
            Vector3 force = Vector3.Zero;
            makeFeelers();            
            List<Sphere> tagged = new List<Sphere>();
            float minBoxLength = 20.0f;
	        float boxLength = minBoxLength + ((fighter.velocity.Length()/fighter.maxSpeed) * minBoxLength * 2.0f);
            
            if (float.IsNaN(boxLength))
            {
                System.Console.WriteLine("NAN");
            }
            // Matt Bucklands Obstacle avoidance
            // First tag obstacles in range
            foreach (Entity child in XNAGame.Instance().Children)
            {
                if (child is Obstacle)
                {
                    Obstacle obstacle = (Obstacle)child;

                    Vector3 toCentre = fighter.pos - obstacle.pos;
                    float dist = toCentre.Length();
                    if (dist < boxLength)
                    {
                        tagged.Add(obstacle);
                    }
                }
            }

            float distToClosestIP = float.MaxValue;
	        Sphere closestIntersectingObstacle = null;
	        Vector3 localPosOfClosestObstacle = Vector3.Zero;
	        Vector3 intersection = Vector3.Zero;

            Matrix localTransform = Matrix.Invert(fighter.worldTransform);
            foreach (Obstacle o in tagged)
            {
                Vector3 localPos = Vector3.Transform(o.pos, localTransform);
                //Vector3 localPos = o.pos - fighter.pos;

		        // If the local position has a positive Z value then it must lay
		        // behind the agent. (in which case it can be ignored)
                if (localPos.Z <=0)
		        {
			        // If the distance from the x axis to the object's position is less
			        // than its radius + half the width of the detection box then there
			        // is a potential intersection.
			        float expandedRadius = fighter.BoundingSphere.Radius + o.Radius;
			        if ((Math.Abs(localPos.Y) < expandedRadius) && (Math.Abs(localPos.X) < expandedRadius))
			        {
				        // Now to do a ray/sphere intersection test. The center of the				
				        // Create a temp Entity to hold the sphere in local space
                        Sphere tempSphere = new Sphere(expandedRadius);
				        tempSphere.pos = localPos;				            

				        // Create a ray
				        Ray ray = new Ray();
				        ray.pos = new Vector3(0, 0, 0);
                        ray.look = fighter.basis;

				        // Find the point of intersection
                        if (tempSphere.closestRayIntersects(ray, Vector3.Zero, ref intersection) == false)
                        {
                            return Vector3.Zero;
                        }

				        // Now see if its the closest, there may be other intersecting spheres
				        float dist = intersection.Length();
				        if (dist < distToClosestIP)
				        {
					        dist = distToClosestIP;
                            closestIntersectingObstacle = o;
					        localPosOfClosestObstacle = localPos;
				        }				
			        }
		        }              
		        if (closestIntersectingObstacle != null)
		        {
			        // Now calculate the force
			        // Calculate Z Axis braking  force
			        float multiplier = 200 * (1.0f + (boxLength - localPosOfClosestObstacle.Z) / boxLength);

                    
			
			        //calculate the lateral force
                    float expandedRadius = fighter.BoundingSphere.Radius + o.Radius;
			        force.X = (expandedRadius - Math.Abs(localPosOfClosestObstacle.X))  * multiplier;

                    force.Y = (expandedRadius - -Math.Abs(localPosOfClosestObstacle.X)) * multiplier;

                    if (localPosOfClosestObstacle.X > 0)
                    {
                        force.X = -force.X;
                    }
                    
                    if (localPosOfClosestObstacle.Y > 0)
                    {
                        force.Y = -force.Y;
                    }

                    /*if (fighter.pos.X < o.pos.X)
                    {
                        force.X = -force.X;
                    }
                     
                    if (fighter.pos.Y < o.pos.Y)
                    {
                        force.Y = -force.Y;
                    }*/
                    
                    Line.DrawLine(fighter.pos, fighter.pos + fighter.look * boxLength, Color.BlueViolet);
			        //apply a braking force proportional to the obstacle's distance from
			        //the vehicle.
			        const float brakingWeight = 40.0f;
                    force.Z = (closestIntersectingObstacle.Radius -
                                       localPosOfClosestObstacle.Z) *
                                       brakingWeight;

			        //finally, convert the steering vector from local to world space
                    force = Vector3.Transform(force, fighter.worldTransform);                    
                }                
            }
             
            fighter.DrawFeelers = false;
            fighter.DrawAxis = false;
            checkNaN(force);
            
            return force;
        }

        static public bool checkNaN(ref Vector3  v, Vector3 def)
        {
            if (float.IsNaN(v.X))
            {
                System.Console.WriteLine("Nan");
                v = def;
                return true;
            }
            if (float.IsNaN(v.Y))
            {
                System.Console.WriteLine("Nan");
                v = def;
                return true;
            }
            if (float.IsNaN(v.Z))
            {
                System.Console.WriteLine("Nan");
                v = def;
                return true;
            }
            return false;
        }

        static public bool checkNaN(Vector3 v)
        {
            if (float.IsNaN(v.X))
            {
                System.Console.WriteLine("Nan");
                return true;
            }
            if (float.IsNaN(v.Y))
            {
                System.Console.WriteLine("Nan");
                return true;
            }
            if (float.IsNaN(v.Z))
            {
                System.Console.WriteLine("Nan");
                return true;
            }
            return false;
        }

        Vector3 offsetPursuit(Vector3 offset)
        {
            Vector3 target = Vector3.Zero;

            //target = offset + fighter.Leader.pos;
            target = Vector3.Transform(offset, fighter.Leader.worldTransform);

            float dist = (target - fighter.pos).Length();     
      
            float lookAhead = (dist / fighter.maxSpeed);

            target = target + (lookAhead * fighter.Leader.velocity);

            checkNaN(target);
            return arrive(target);
        }

        Vector3 pursue()
        {
            float dist = (fighter.Target.pos - fighter.pos).Length();

            if (dist < 1.0f)
            {
                //fighter.Target.pos = new Vector3(20, 20, 0);
            }
            float lookAhead = (dist / fighter.maxSpeed);

            Vector3 target = fighter.Target.pos + (lookAhead * fighter.Target.velocity);
            sphere.pos = target;          
            return seek(target);
        }

        Vector3 flee(Vector3 targetPos)
        {
            float panicDistance = 20.0f;
            Vector3 desiredVelocity;
            desiredVelocity = fighter.pos - targetPos;
            if (desiredVelocity.Length() > panicDistance)
            {
                return Vector3.Zero;
            }
            desiredVelocity.Normalize();
            desiredVelocity *= fighter.maxSpeed;

            sphere.pos = fighter.targetPos;
            return (desiredVelocity - fighter.velocity);
        }

        Vector3 seek(Vector3 targetPos)
        {           
            Vector3 desiredVelocity;

            desiredVelocity = targetPos - fighter.pos;
            desiredVelocity.Normalize();
            desiredVelocity *= fighter.maxSpeed;

            sphere.pos = fighter.targetPos;
            return (desiredVelocity - fighter.velocity);
        }

        float randomClamped()
        {
            return 1.0f - ((float) random.NextDouble() * 2.0f); 
        }      

        Vector3 wander()
        {

            float wanderRadius = 5.2f;
            float wanderDistance = 10.0f;
            float wanderJitter = 40.0f;

            float jitterTimeSlice = wanderJitter * timeDelta;

            wanderTarget += new Vector3(randomClamped() * jitterTimeSlice, randomClamped() * jitterTimeSlice, randomClamped() * jitterTimeSlice);
            wanderTarget.Normalize();

            wanderTarget = wanderTarget * wanderRadius;

            Vector3 basis = fighter.basis;

            Vector3 worldTarget = (basis * wanderDistance) + wanderTarget;

            worldTarget = Vector3.Transform(worldTarget, fighter.worldTransform);

            sphere.pos = worldTarget;
            return (worldTarget - fighter.pos);

        }

        public Vector3 wall_avoidance()
        {
            makeFeelers();

            Plane worldPlane = new Plane(new Vector3(0, 1, 0), 0);
            Vector3 force = Vector3.Zero;

            foreach (Vector3 feeler in fighter.Feelers)
            {
                float dot = worldPlane.DotCoordinate(feeler);
                if (dot < 0)
                {
                    float distance = Math.Abs(dot - worldPlane.D);
                    force += worldPlane.Normal * distance;
                }           
            }

            if (force.Length() > 0.0)
            {
                fighter.DrawFeelers = true;
            }
            else
            {
                fighter.DrawFeelers = false;
            }
            fighter.DrawAxis = false;
            return force;
        }

        private void makeFeelers()
        {
 	        fighter.Feelers.Clear();
            float feelerDistance = 20.0f;
            // Make the forward feeler
            Vector3 newFeeler = fighter.basis * feelerDistance;
            newFeeler = Vector3.Transform(newFeeler, fighter.worldTransform);
            fighter.Feelers.Add(newFeeler);

            newFeeler = fighter.basis * feelerDistance;
            newFeeler = Vector3.Transform(newFeeler, Matrix.CreateRotationY(MathHelper.PiOver4));
            newFeeler = Vector3.Transform(newFeeler, fighter.worldTransform);
            fighter.Feelers.Add(newFeeler);
            newFeeler = fighter.basis * feelerDistance;
            newFeeler = Vector3.Transform(newFeeler, Matrix.CreateRotationY(- MathHelper.PiOver4));
            newFeeler = Vector3.Transform(newFeeler, fighter.worldTransform);
            fighter.Feelers.Add(newFeeler);

            newFeeler = fighter.basis * feelerDistance;
            newFeeler = Vector3.Transform(newFeeler, Matrix.CreateRotationX(MathHelper.PiOver4));
            newFeeler = Vector3.Transform(newFeeler, fighter.worldTransform);
            fighter.Feelers.Add(newFeeler);
            newFeeler = fighter.basis * feelerDistance;
            newFeeler = Vector3.Transform(newFeeler, Matrix.CreateRotationX(-MathHelper.PiOver4));
            newFeeler = Vector3.Transform(newFeeler, fighter.worldTransform);
            fighter.Feelers.Add(newFeeler);
        }


        public Vector3 arrive(Vector3 target)
        {
            Vector3 toTarget = target - fighter.pos;
            
            float slowingDistance = 8.0f;
            float distance = toTarget.Length();
            if (distance == 0.0f)
            {
                return Vector3.Zero;
            }
            const float DecelerationTweaker = 10.3f;
            float ramped = fighter.maxSpeed * (distance / (slowingDistance * DecelerationTweaker));

            float clamped = Math.Min(ramped, fighter.maxSpeed);
            sphere.pos = fighter.targetPos;
            Vector3 desired = clamped * (toTarget / distance);

            checkNaN(desired);
          

            return desired - fighter.velocity;
        }

        public Vector3 calculate()
        {
            if (calculationMethod == CalculationMethods.WeightedTruncatedSum)
            {
                return calculateWeightedTruncatedSum();
            }
            if (calculationMethod == CalculationMethods.WeightedTruncatedRunningSumWithPrioritisation)
            {
                return calculateWeightedPrioritised();
            }

            return Vector3.Zero;            
        }

        private Vector3 calculateWeightedPrioritised()
        {
            Vector3 force = Vector3.Zero;
            Vector3 steeringForce = Vector3.Zero;

            
            if (isOn(behaviour_type.obstacle_avoidance))
            {
                force = obstacleAvoidance() * weights[behaviour_type.obstacle_avoidance];
                if (!accumulateForce(ref steeringForce, force))
                {
                    return steeringForce;
                }
            }
            if (isOn(behaviour_type.wall_avoidance))
            {
                force = wall_avoidance() * weights[behaviour_type.wall_avoidance];
                if (!accumulateForce(ref steeringForce, force))
                {
                    return steeringForce;
                }
            }
            if (isOn(behaviour_type.evade))
            {
                force = evade() * weights[behaviour_type.evade];
                if (!accumulateForce(ref steeringForce, force))
                {
                    return steeringForce;
                }
            }
            if (isOn(behaviour_type.arrive))
            {
                force = arrive(fighter.targetPos) * weights[behaviour_type.arrive];
                if (!accumulateForce(ref steeringForce, force))
                {
                    return steeringForce;
                }
            }
            if (isOn(behaviour_type.seek))
            {
                force = seek(fighter.targetPos) * weights[behaviour_type.seek];
                if (!accumulateForce(ref steeringForce, force))
                {
                    return steeringForce;
                }
            }
            if (isOn(behaviour_type.wander))
            {
                force = wander() * weights[behaviour_type.wander];
                if (!accumulateForce(ref steeringForce, force))
                {
                    return steeringForce;
                }
            }
            if (isOn(behaviour_type.pursuit))
            {
                force = pursue() * weights[behaviour_type.pursuit];
                if (!accumulateForce(ref steeringForce, force))
                {
                    return steeringForce;
                }
            }
            if (isOn(behaviour_type.offset_pursuit))
            {
                force = offsetPursuit(fighter.offset) * weights[behaviour_type.offset_pursuit];
                if (!accumulateForce(ref steeringForce, force))
                {
                    return steeringForce;
                }
            }
            if (isOn(behaviour_type.follow_path))
            {
                force = followPath() * weights[behaviour_type.follow_path];
                if (!accumulateForce(ref steeringForce, force))
                {
                    return steeringForce;
                }
            }
            
            return steeringForce;
        }

        private Vector3 followPath()
        {
            float epsilon = 5.0f;
            float dist = (fighter.pos - fighter.Path.NextWaypoint()).Length();
            if (dist < epsilon)
            {
                fighter.Path.AdvanceToNext();
            }
            if ((! fighter.Path.Looped) && fighter.Path.IsLast())
            {
                return arrive(fighter.Path.NextWaypoint());
            }
            else
            {
                return seek(fighter.Path.NextWaypoint());
            }
       }

        private bool accumulateForce(ref Vector3 runningTotal, Vector3 force)
        {
            float soFar = runningTotal.Length();

            float remaining = maxForce - soFar;
            if (remaining <= 0)
            {
                return false;
            }

            float toAdd = force.Length();
           

            if (toAdd < remaining)
            {
                runningTotal += force;
            }
            else
            {
                runningTotal += Vector3.Normalize(force) * remaining;
                return false;
            }
            return true;
        }

        private Vector3 calculateWeightedTruncatedSum()
        {
            Vector3 force = Vector3.Zero;

            if (isOn(behaviour_type.seek))
            {
                force += seek(fighter.targetPos) * weights[behaviour_type.seek];
            }
            if (isOn(behaviour_type.wall_avoidance))
            {
                force += wall_avoidance() * weights[behaviour_type.wall_avoidance];
            }
            if (isOn(behaviour_type.wander))
            {
                force += wander() * weights[behaviour_type.wander];
            }

            if (isOn(behaviour_type.pursuit))
            {
                force += pursue() * weights[behaviour_type.pursuit];
            }

            if (isOn(behaviour_type.arrive))
            {
                force += arrive(fighter.targetPos) * weights[behaviour_type.arrive];
            }
            if (force.Length() > maxForce)
            {
                force.Normalize();
                force = force * maxForce;
            }
            return force;
        }
    }
}
