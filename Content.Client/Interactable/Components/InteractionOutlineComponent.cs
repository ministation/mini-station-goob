// SPDX-License-Identifier: MIT

using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.Interactable.Components
{
    [RegisterComponent]
    public sealed partial class InteractionOutlineComponent : Component
    {
        private static readonly ProtoId<ShaderPrototype> ShaderInRange = "SelectionOutlineInrange";
        private static readonly ProtoId<ShaderPrototype> ShaderOutOfRange = "SelectionOutline";

        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IEntityManager _entMan = default!;

        private const float DefaultWidth = 1;

        private bool _inRange;
        private ShaderInstance? _shader;
        private int _lastRenderScale;

        public void OnMouseEnter(EntityUid uid, bool inInteractionRange, int renderScale)
        {
            if (!_entMan.TryGetComponent(uid, out SpriteComponent? sprite) || sprite.PostShader != null)
                return;

            EnsureShader(inInteractionRange, renderScale);
            sprite.PostShader = _shader;
        }

        public void OnMouseLeave(EntityUid uid)
        {
            if (_entMan.TryGetComponent(uid, out SpriteComponent? sprite))
            {
                if (sprite.PostShader == _shader)
                    sprite.PostShader = null;
                sprite.RenderOrder = 0;
            }

            _shader?.Dispose();
            _shader = null;
        }

        public void UpdateInRange(EntityUid uid, bool inInteractionRange, int renderScale)
        {
            if (!_entMan.TryGetComponent(uid, out SpriteComponent? sprite) || sprite.PostShader != _shader)
                return;

            if (inInteractionRange == _inRange && _lastRenderScale == renderScale)
                return;

            EnsureShader(inInteractionRange, renderScale);
            sprite.PostShader = _shader;
        }

        /// <summary>
        /// Reuse the existing shader instance when only width changes; recreate when
        /// switching in-range / out-of-range prototypes.
        /// </summary>
        private void EnsureShader(bool inRange, int renderScale)
        {
            if (_shader != null && _inRange == inRange)
            {
                if (_lastRenderScale != renderScale)
                {
                    _lastRenderScale = renderScale;
                    _shader.SetParameter("outline_width", DefaultWidth * renderScale);
                }

                return;
            }

            _shader?.Dispose();
            _inRange = inRange;
            _lastRenderScale = renderScale;
            _shader = MakeNewShader(inRange, renderScale);
        }

        private ShaderInstance MakeNewShader(bool inRange, int renderScale)
        {
            var shaderName = inRange ? ShaderInRange : ShaderOutOfRange;

            var instance = _prototypeManager.Index(shaderName).InstanceUnique();
            instance.SetParameter("outline_width", DefaultWidth * renderScale);
            return instance;
        }
    }
}
