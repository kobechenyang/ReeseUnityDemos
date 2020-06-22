﻿using Reese.Nav;
using Reese.Random;
using Unity.Entities;
using Unity.Jobs;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine.SceneManagement;

namespace Reese.Demo
{
    class NavDestinationSystem : SystemBase
    {
        EntityCommandBufferSystem barrier => World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();

        protected override void OnUpdate()
        {
            if (
                !SceneManager.GetActiveScene().name.Equals("NavPerformanceDemo") &&
                !SceneManager.GetActiveScene().name.Equals("NavMovingJumpDemo")
            ) return;

            var commandBuffer = barrier.CreateCommandBuffer().ToConcurrent();
            var jumpableBufferFromEntity = GetBufferFromEntity<NavJumpableBufferElement>(true);
            var renderBoundsFromEntity = GetComponentDataFromEntity<RenderBounds>(true);
            var localToWorldFromEntity = GetComponentDataFromEntity<LocalToWorld>(true);
            var randomArray = World.GetExistingSystem<RandomSystem>().RandomArray;

            Entities
                .WithNone<NavNeedsDestination>()
                .WithReadOnly(jumpableBufferFromEntity)
                .WithReadOnly(renderBoundsFromEntity)
                .WithReadOnly(localToWorldFromEntity)
                .WithNativeDisableParallelForRestriction(randomArray)
                .ForEach((Entity entity, int entityInQueryIndex, int nativeThreadIndex, ref NavAgent agent, in Parent surface) =>
                {
                    if (
                        surface.Value.Equals(Entity.Null) ||
                        !jumpableBufferFromEntity.Exists(surface.Value)
                    ) return;

                    var jumpableSurfaces = jumpableBufferFromEntity[surface.Value];
                    var random = randomArray[nativeThreadIndex];

                    if (jumpableSurfaces.Length == 0)
                    { // For the NavPerformanceDemo scene.
                        commandBuffer.AddComponent(entityInQueryIndex, entity, new NavNeedsDestination{
                            Destination = NavUtil.GetRandomPointInBounds(
                                ref random,
                                renderBoundsFromEntity[surface.Value].Value,
                                20
                            )
                        });
                    }
                    else
                    { // For the NavMovingJumpDemo scene.
                        var destinationSurface = jumpableSurfaces[random.NextInt(0, jumpableSurfaces.Length)];

                        var localPoint = NavUtil.GetRandomPointInBounds(
                            ref random,
                            renderBoundsFromEntity[destinationSurface].Value,
                            3
                        );

                        var worldPoint = NavUtil.MultiplyPoint3x4(
                            localToWorldFromEntity[destinationSurface.Value].Value,
                            localPoint
                        );

                        commandBuffer.AddComponent(entityInQueryIndex, entity, new NavNeedsDestination{
                            Destination = worldPoint
                        });
                    }

                    randomArray[nativeThreadIndex] = random;
                })
                .WithName("NavDestinationJob")
                .ScheduleParallel();

            barrier.AddJobHandleForProducer(Dependency);
        }
    }
}
