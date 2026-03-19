using System;
using System.Collections.Generic;

namespace MH.Core
{

    public class Entity
    {
        public Dictionary<Type, EntityComponent> Components { get; } = new ();

        public void AddComponent<T>(T component) where T : EntityComponent
        {
            if (Components.ContainsKey(typeof(T)))
            {
                throw new Exception($"Component of type {typeof(T)} already exists");
            }
            Components.Add(typeof(T), component);
        }

        public T GetComponent<T>() where T : EntityComponent
        {
            if (!Components.ContainsKey(typeof(T)))
            {
                throw new Exception($"Component of type {typeof(T)} does not exist");
            }
            return (T)Components[typeof(T)];
        }

        public void RemoveComponent<T>() where T : EntityComponent
        {
            if (!Components.ContainsKey(typeof(T)))
            {
                throw new Exception($"Component of type {typeof(T)} does not exist");
            }
            Components.Remove(typeof(T));
        }

        public virtual void Tick(float deltaTime){
            foreach(var component in Components.Values){
                component.Tick(deltaTime);
            }
        }
    }

    public class EntityComponent
    {
        public Entity Entity { get; }

        public EntityComponent(Entity entity)
        {
            Entity = entity;
        }

        public virtual void Tick(float deltaTime){

        }

    }

    public class Root2D : EntityComponent
    {

        public CustomVector2 Position ;
        public Root2D(Entity entity) : base(entity)
        {
            
        }
    }

    // public class Root3D : EntityComponent
    // {

    //     public Vector3 Position { get; set; }
    //     public Root3D(Entity entity) : base(entity)
    //     {
    //     }
    // }
}