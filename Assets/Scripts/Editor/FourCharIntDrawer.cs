using UnityEngine;
using UnityEditor;
using System.Text;

namespace MediaMonks
{
	[CustomPropertyDrawer(typeof(OpenFontFormat.Tag))]
	public class FourCharIntDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			string name = OpenFontFormat.Convert.UIntToFourChar((uint)property.intValue);

			var labelRect = new Rect(position.x, position.y, position.width * 0.5f, position.height);
			var intRect = new Rect(position.x + position.width * 0.5f, position.y, position.width * 0.5f, position.height);

			EditorGUI.LabelField(labelRect, label, new GUIContent( name));

			property.intValue = EditorGUI.IntField(intRect, GUIContent.none, property.intValue);
		}

	}
}