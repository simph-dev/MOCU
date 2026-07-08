using UnityEngine.UIElements;


namespace CustomUxmlElements
{
    public class CustomImage : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<CustomImage, UxmlTraits> { }

        private CustomImage _container;
        private VisualElement _imageShape;
        private VisualElement _imageColor;

        public CustomImage()
        {
            _container = this;
            _imageShape = new();
            _imageColor = new();

            _container.AddToClassList("custom-image-container");
            _imageShape.AddToClassList("custom-image-shape");
            _imageColor.AddToClassList("custom-image-color");

            _imageShape.Add(_imageColor);
            _container.Add(_imageShape);
        }
    }
}


//_container.RegisterCallback<GeometryChangedEvent>(evt => ApplyStyles());
/*private void ApplyStyles()
        {
            *//*var initialImage = _container.resolvedStyle.backgroundImage;
            var desiredImage = _imageShape.resolvedStyle.backgroundImage;
            var empty = new StyleBackground();

            _imageShape.style.backgroundImage = _container.resolvedStyle.backgroundImage;
            //_container.style.backgroundImage = new StyleBackground();

            var iconColorValue = _container.resolvedStyle.color;
            _imageShape.style.backgroundColor = iconColorValue;*/


/*if (desiredImage != initialImage && initialImage != empty)
{
    _imageShape.style.backgroundImage = initialImage;
    _container.style.backgroundImage = empty;
}

if (desiredImage != initialImage && initialImage != empty)
{
    _imageShape.style.backgroundImage = initialImage;
    _container.style.backgroundImage = empty;
}*/

/*var iconColorValue = _container.resolvedStyle.color;
_imageShape.style.backgroundColor = iconColorValue;

var containerWidth = _container.resolvedStyle.width;
var containerHeight = _container.resolvedStyle.height;
var containerPadding = _container.resolvedStyle.paddingLeft; // it is assumed that it is the same from all sides

var iconShapeWidth = containerWidth - 2 * containerPadding;
var iconShapeHeight = containerHeight - 2 * containerPadding;

_imageShape.style.width = iconShapeWidth;
_imageShape.style.height = iconShapeHeight;


_imageShape.style.backgroundImage = initialImage;
_container.style.backgroundImage = empty;*/

/*if (desiredImage != initialImage && initialImage != empty)
{
    _imageShape.style.backgroundImage = initialImage;
    _container.style.backgroundImage = empty;
}*/


/*var containerWidth = _container.resolvedStyle.width;
var containerHeight = _container.resolvedStyle.height;
var containerPadding = _container.resolvedStyle.paddingLeft; // it is assumed that it is the same from all sides
var iconColorValue = _container.resolvedStyle.color;   // not font but image color

var iconShapeWidth = containerWidth - 2 * containerPadding;
var iconShapeHeight = containerHeight - 2 * containerPadding;

_imageShape.style.width = iconShapeWidth;
_imageShape.style.height = iconShapeHeight;
_imageShape.style.backgroundColor = iconColorValue;*/

/*_iconShape.style.backgroundImage = _iconContainer.resolvedStyle.backgroundImage;
_iconContainer.style.backgroundImage = new StyleBackground();

_iconShape.style.backgroundImage = _iconContainer.resolvedStyle.backgroundImage;
_iconContainer.style.backgroundImage = new StyleBackground();

_iconShape.style.backgroundImage = _iconContainer.resolvedStyle.backgroundImage;
_iconContainer.style.backgroundImage = new StyleBackground();*/

/*_iconShape.style.width = new StyleLength(Length.Percent(100));
_iconShape.style.height = new StyleLength(Length.Percent(100));

_iconShape.resolvedStyle.GetProperty("--custom-image-url")

_iconShape.MarkDirtyRepaint();*//*

//_iconShape.style.backgroundImage = new StyleBackground(Resources.Load<VectorImage>("IntercomActiveIcon"));

*//*var containerWidth = _iconContainer.resolvedStyle.width;
var containerHeight = _iconContainer.resolvedStyle.height;
var containerPadding = _iconContainer.resolvedStyle.paddingLeft; // it is assumed that it is the same from all sides
var iconColorValue = _iconContainer.resolvedStyle.color;   // not font but image color

var iconShapeWidth = containerWidth - 2 * containerPadding;
var iconShapeHeight = containerHeight - 2 * containerPadding;

_iconShape.style.width = iconShapeWidth;
_iconShape.style.height = iconShapeHeight;
_iconColor.style.backgroundColor = iconColorValue;*//*

//var imageFromUss = _iconContainer.resolvedStyle.backgroundImage;
//var imageFromLoad = Resources.Load<VectorImage>("IntercomActiveIcon");

//Debug.Log($"_iconShape: {_iconShape.resolvedStyle.width}, _iconContainer: {_iconContainer.resolvedStyle.width}, containerPadding: {containerPadding}");
*//*if (image == null)
    _iconShape.style.backgroundImage = new StyleBackground(Resources.Load<VectorImage>("IntercomActiveIcon"));
else
    _iconShape.style.backgroundImage = image;*/


/*UnityEngine.Debug.Log($"before: {image}");

if (image == null)  // || image.texture == null || image.vectorImage == null
{
    var imageResource = Resources.Load<VectorImage>("IntercomActiveIcon.svg");
    _iconShape.style.backgroundImage = new StyleBackground(imageResource);
    UnityEngine.Debug.Log($"if: {imageResource}");
}
else
{
    _iconShape.style.backgroundImage = image;
    UnityEngine.Debug.Log($"else: {image}");
}*//*
}*/