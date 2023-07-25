import React from "react";
import "./SplashPage.css";
import logo from "../images/TP_WEB_logo_black.png";
import { Pivot, PivotItem } from "@fluentui/react";
import { Stack } from "@fluentui/react/lib/Stack";
import { Text } from "@fluentui/react/lib/Text";
import { Separator } from "@fluentui/react/lib/Separator";
import { DefaultButton } from "@fluentui/react/lib/Button";

const stackTokens = { childrenGap: 12 };

const VerticalSeparatorStack = (props) => (
  <Stack horizontal horizontalAlign="space-evenly">
    {React.Children.map(props.children, (child) => {
      return (
        <Stack horizontalAlign="center" tokens={stackTokens}>
          {child}
        </Stack>
      );
    })}
  </Stack>
);

const contentText = "Lorem ipsum dolor sit amet, cu ocurreret definiebas sit, eu latine appareat volutpat nam, id per ipsum maluisset. Ad mel hinc libris urbanitas, te vix illum veritus, no sea facilisis laboramus. Mel quaestio laboramus ut, tota democritum eam ne, error delenit senserit vel an. At est postea labore salutandi, his cu aeque labores invenire. Te populo suscipit mea. Id vel expetendis consequuntur, ne simul interesset eloquentiam quo.";

class SplashPage extends React.Component {
  _openVideo() {
    window.open("https://www.youtube.com/watch?v=jFNR5uzKxAY&ab_channel=TerraPraxis", "_blank");
  }

  render() {
    return (
      <div>
        <div className="header">
          <img className="logo" src={logo} alt="TP Logo" />
        </div>

        <Pivot
          className="toolbar"
          aria-label="Basic Pivot Example"
          linkSize="large"
        >
          <PivotItem headerText="Welcome">
            <Stack verticalAlign="center" className="centerContent">
              <Text variant="xxLarge" className="title">
                Repowering Coal
              </Text>
              <Text className="textSection">{contentText}</Text>
              <div>
                <iframe
                  width="560"
                  height="315"
                  src="https://www.youtube.com/embed/jFNR5uzKxAY"
                  title="YouTube video player"
                  frameborder="0"
                  allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
                  allowfullscreen
                ></iframe>
              </div>
            </Stack>
          </PivotItem>

          <PivotItem headerText="Dashboard">
            <iframe
              width="1140"
              height="541.25"
              src="https://msit.powerbi.com/reportEmbed?reportId=282a41f0-9278-46e0-98c8-d754c81f6fd1&autoAuth=true&ctid=72f988bf-86f1-41af-91ab-2d7cd011db47&config=eyJjbHVzdGVyVXJsIjoiaHR0cHM6Ly9kZi1tc2l0LXNjdXMtcmVkaXJlY3QuYW5hbHlzaXMud2luZG93cy5uZXQvIn0%3D"
              frameborder="0"
              allowFullScreen="true"
            ></iframe>
          </PivotItem>

          <PivotItem headerText="Survey">
            <iframe
              width="640px"
              height="600px"
              src="https://forms.office.com/Pages/ResponsePage.aspx?id=v4j5cvGGr0GRqy180BHbR89Z-ZFs0hRGshdzisQsyF9UNE5TQ1FNTzhPTE1JS0ZLMUkwUlZRVkNNVC4u&embed=true"
              frameborder="0"
              marginwidth="0"
              marginheight="0"
              // style="border: none; max-width:100%; max-height:100 vh"
              allowfullscreen
              webkitallowfullscreen
              mozallowfullscreen
              msallowfullscreen
            >
              {" "}
            </iframe>
          </PivotItem>

          <PivotItem headerText="Dive Deep">
            <VerticalSeparatorStack>
              <div className="diveDeepSection">
                <Text variant="xLargePlus" className="title">
                  Carbon Emissions
                </Text>
                <Text className="textSection">{contentText}</Text>
                <Text className="textSection">{contentText}</Text>
                <DefaultButton
                  text="Learn More"
                  onClick={this._openVideo}
                  allowDisabledFocus
                  className="button"
                />
              </div>

              <>
                <Stack.Item className="vertical">
                  <Separator vertical />
                </Stack.Item>
              </>

              <div className="diveDeepSection">
                <Text variant="xLargePlus" className="title">
                  Value
                </Text>
                <Text className="textSection">{contentText}</Text>
                <Text className="textSection">{contentText}</Text>
                <DefaultButton
                  text="Learn More"
                  onClick={this._openVideo}
                  allowDisabledFocus
                  className="button"
                />
              </div>

              <>
                <Stack.Item className="vertical">
                  <Separator vertical />
                </Stack.Item>
              </>

              <div className="diveDeepSection">
                <Text variant="xLargePlus" className="title">
                  Plant Owners
                </Text>
                <Text className="textSection">{contentText}</Text>
                <Text className="textSection">{contentText}</Text>
                <DefaultButton
                  text="Learn More"
                  onClick={this._openVideo}
                  allowDisabledFocus
                  className="button"
                />
              </div>
            </VerticalSeparatorStack>
          </PivotItem>
        </Pivot>
      </div>
    );
  }
}

export default SplashPage;