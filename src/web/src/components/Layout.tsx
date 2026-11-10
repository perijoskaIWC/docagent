import { Tab, TabList, Title3, makeStyles, tokens } from '@fluentui/react-components'
import { AddRegular, ChatRegular, LibraryRegular, TextBulletListLtrRegular } from '@fluentui/react-icons'
import { Outlet, useLocation, useNavigate } from 'react-router-dom'

const useStyles = makeStyles({
  header: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalXXL,
    padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalXXL}`,
    borderBottom: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke2}`,
    backgroundColor: tokens.colorNeutralBackground1,
  },
  main: { padding: `${tokens.spacingVerticalXL} ${tokens.spacingHorizontalXXL}`, maxWidth: '1280px' },
})

const sections = [
  { path: '/runs/new', label: 'New run', icon: <AddRegular /> },
  { path: '/', label: 'Runs', icon: <TextBulletListLtrRegular /> },
  { path: '/ask', label: 'Ask', icon: <ChatRegular /> },
  { path: '/knowledge', label: 'Knowledge', icon: <LibraryRegular /> },
]

/** Which nav tab a path belongs to: a run's own page counts as "Runs". */
function sectionOf(pathname: string): string | undefined {
  if (pathname === '/runs/new') return '/runs/new'
  if (pathname === '/' || pathname.startsWith('/runs/')) return '/'
  return sections.find((section) => section.path !== '/' && pathname.startsWith(section.path))?.path
}

export function Layout() {
  const styles = useStyles()
  const navigate = useNavigate()
  const { pathname } = useLocation()

  return (
    <>
      <header className={styles.header}>
        <Title3>DocAgent</Title3>
        <nav>
          <TabList selectedValue={sectionOf(pathname)} onTabSelect={(_, data) => navigate(String(data.value))}>
            {sections.map((section) => (
              <Tab key={section.path} value={section.path} icon={section.icon}>
                {section.label}
              </Tab>
            ))}
          </TabList>
        </nav>
      </header>
      <main className={styles.main}>
        <Outlet />
      </main>
    </>
  )
}
