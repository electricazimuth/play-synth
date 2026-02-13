using UnityEngine;
using UnityEngine.UIElements;


using TriGrid.Unity;
 


namespace Azimuth
{
    public class HomeMenu : MonoBehaviour {

        public UIDocument document;
        public TriGrid.Unity.GridController gridController;
        protected VisualElement documentRoot;
        private RadioButtonGroup editMode;


        protected void Awake() {

            
        }
        protected void OnEnable(){

            


            document = GetComponent<UIDocument>();
            documentRoot = document.rootVisualElement;
            editMode = documentRoot.Q<RadioButtonGroup>(name: "editmode");
            editMode.RegisterValueChangedCallback(EditModeChanged);

        }

        private void EditModeChanged( ChangeEvent<int> changeEvt ){
            //Debug.Log(changeEvt.previousValue);
            //Debug.Log(changeEvt.newValue);
            
            if(gridController != null)
            {
                gridController.CurrentTool = RadioIndexToGridTool(changeEvt.newValue);
            }
        }

        private TriGrid.Unity.GridTool RadioIndexToGridTool(int index)
        {
            switch (index)
            {
                case 0:
                    return GridTool.AddFace;
                    break;
                case 1:
                    return GridTool.RemoveFace;
                    break;
                case 2:
                    return GridTool.PlaceEmitter;
                    break;
                case 3:
                    return GridTool.PlaceReflector;
                    break;
            }
            return GridTool.AddFace;
        }

    }

}